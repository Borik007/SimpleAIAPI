using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SimpleAIAPI.Data;
using SimpleAIAPI.Models;
using SimpleAIAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Ensure the OLLAMA_IP is read from environment or config
var ollamaIp = builder.Configuration["OLLAMA_IP"] ?? "ollama:11434";

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OllamaJsonSerializerContext.Default);
});

builder.Services.AddDbContext<AIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<IOllamaService, OllamaService>();

var app = builder.Build();

// Ensure the AI Server exists in the DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AIDbContext>();
    db.Database.EnsureCreated();
    var server = await db.AIServers.FindAsync(ollamaIp);
    if (server == null)
    {
        db.AIServers.Add(new AIServer { AI_IP = ollamaIp });
        await db.SaveChangesAsync();
    }
}

var api = app.MapGroup("/api");

// /api/newchat -> blank response: userID
api.MapGet("/newchat", async (AIDbContext db, HttpContext context) =>
{
    var clientId = Guid.NewGuid();
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    var client = new Client
    {
        ClientID = clientId,
        Client_IP = clientIp,
        AI_IP = ollamaIp,
        Model = "llama3"
    };

    db.Clients.Add(client);
    await db.SaveChangesAsync();

    return Results.Ok(clientId.ToString());
});

// /api/model/{userID}:{model} -> response: http code ok
api.MapGet("/model/{userID}:{model}", async (Guid userID, string model, AIDbContext db) =>
{
    var client = await db.Clients.FindAsync(userID);
    if (client == null) return Results.NotFound("Client not found");

    client.Model = model;
    await db.SaveChangesAsync();

    return Results.Ok();
});

// /api/message/{userID}:{message} -> response: message
api.MapGet("/message/{userID}:{message}", async (Guid userID, string message, AIDbContext db, IOllamaService ollama) =>
{
    var client = await db.Clients
        .Include(c => c.Messages)
        .FirstOrDefaultAsync(c => c.ClientID == userID);

    if (client == null) return Results.NotFound("Client not found");

    // Add user message to history
    var userMsg = new ChatMessage 
    { 
        Role = "user", 
        Message = message, 
        ClientID = userID,
        Timestamp = DateTime.UtcNow 
    };
    db.Messages.Add(userMsg);
    await db.SaveChangesAsync();

    // Prepare messages for Ollama
    var ollamaMessages = client.Messages
        .OrderBy(m => m.Timestamp)
        .Select(m => new OllamaMessage(m.Role, m.Message))
        .ToList();

    // Call Ollama
    var responseMessage = await ollama.ChatAsync(client.Model, ollamaMessages);

    // Add assistant response to history
    var assistantMsg = new ChatMessage 
    { 
        Role = "assistant", 
        Message = responseMessage, 
        ClientID = userID,
        Timestamp = DateTime.UtcNow 
    };
    db.Messages.Add(assistantMsg);
    await db.SaveChangesAsync();

    return Results.Ok(responseMessage);
});

app.Run();
