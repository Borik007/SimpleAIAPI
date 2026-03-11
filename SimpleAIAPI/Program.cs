using Microsoft.EntityFrameworkCore;
using SimpleAIAPI.Data;
using SimpleAIAPI.Models;
using SimpleAIAPI.Services;
using SimpleAIAPI.Helpers;


ServerCache cachedServer;

var builder = WebApplication.CreateBuilder(args);

var ollamaIp = builder.Configuration["OLLAMA_IP"] ?? "ollama:11434";
var cacheSize = int.Parse(builder.Configuration["CACHE_SIZE"] ?? "10");

cachedServer = new ServerCache(ollamaIp, cacheSize);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OllamaJsonSerializerContext.Default);
});

builder.Services.AddDbContext<AIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    client.BaseAddress = new Uri($"http://{ollamaIp}/");
    client.Timeout = TimeSpan.FromSeconds(100); 
});


var app = builder.Build();

// Ensure the AI Server exists in the DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AIDbContext>();
    db.Database.EnsureCreated();
    
    var serverExists = await db.AIServers.AnyAsync(s => s.AI_IP == ollamaIp);
    if (!serverExists)
    {
        db.AIServers.Add(new AIServer { AI_IP = ollamaIp });
        try 
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            
        }
    }
}

var api = app.MapGroup("/api");

// /api/newchat -> returns userID
api.MapPost("/newchat", async (AIDbContext db, HttpContext context) =>
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
    
    await cachedServer.SaveClient(client, db);

    return Results.Content(clientId.ToString(), "text/plain");
});

// /api/model -> response: http code ok
api.MapPut("/model", async (HttpContext context, AIDbContext db) =>
{
    
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    
    var parts = body.Split(':', 2);
    if (parts.Length < 2)
        return Results.BadRequest("Invalid format. Use userID:message");
    
    if (!Guid.TryParse(parts[0].Trim(), out var userId))
        return Results.BadRequest("Invalid userID format");
    
    var model = parts[1].Trim();
    var client = await cachedServer.GetClient(userId, db);
    if (client == null) return Results.NotFound("Client not found");

    client.Model = model;
    await cachedServer.SaveClient(client, db);

    return Results.Content("OK", "text/plain");
});

// /api/message -> body: userID:message -> response: message
api.MapPost("/message", async (HttpContext context, AIDbContext db, IOllamaService ollama) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    
    var parts = body.Split(':', 2);
    if (parts.Length < 2)
        return Results.BadRequest("Invalid format. Use userID:message");
    
    if (!Guid.TryParse(parts[0].Trim(), out var userId))
        return Results.BadRequest("Invalid userID format");

    var message = parts[1].Trim();
    if (string.IsNullOrWhiteSpace(message))
        return Results.BadRequest("Message cannot be empty");

    var client = await cachedServer.GetClient(userId, db);

    if (client == null) return Results.NotFound("Client not found");

    // Add user message to history
    var userMsg = new ChatMessage 
    { 
        Role = "user", 
        Message = message, 
        ClientID = userId,
        Timestamp = DateTime.UtcNow 
    };
    client.Messages.Add(userMsg);
    await cachedServer.SaveClient(client, db);

    // Prepare messages for Ollama (limit history to last 10 messages for efficiency)
    var ollamaMessages = client.Messages
        .OrderByDescending(m => m.Timestamp)
        .Take(11) // User's new message + last 10
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
        ClientID = userId,
        Timestamp = DateTime.UtcNow 
    };
    client.Messages.Add(assistantMsg);
    
    //avoid waiting for the save to complete
    cachedServer.SaveClient(client, db).FireAndForget();
    
    return Results.Content(responseMessage, "text/plain");
});

app.Run();
