using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SimpleAIAPI.Models;

public class AIServer
{
    [Key]
    public string AI_IP { get; set; } = string.Empty;
    public List<Client> Clients { get; set; } = new();
}

public class Client
{
    [Key]
    public Guid ClientID { get; set; }
    public string Client_IP { get; set; } = string.Empty;
    public string Model { get; set; } = "llama3"; // Default model if not set
    public List<ChatMessage> Messages { get; set; } = new();

    public string AI_IP { get; set; } = string.Empty;
    public AIServer AIServer { get; set; } = null!;
}

public class ChatMessage
{
    [Key]
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid ClientID { get; set; }
    public Client Client { get; set; } = null!;
}
