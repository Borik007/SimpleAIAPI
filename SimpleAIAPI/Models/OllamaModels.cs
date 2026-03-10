using System.Text.Json.Serialization;
using SimpleAIAPI.Models;

namespace SimpleAIAPI.Models;

public record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream = false);

public record OllamaMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public record OllamaChatResponse(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("message")] OllamaMessage Message,
    [property: JsonPropertyName("done")] bool Done);

[JsonSerializable(typeof(OllamaChatRequest))]
[JsonSerializable(typeof(OllamaChatResponse))]
[JsonSerializable(typeof(OllamaMessage))]
[JsonSerializable(typeof(List<OllamaMessage>))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
internal partial class OllamaJsonSerializerContext : JsonSerializerContext
{
}
