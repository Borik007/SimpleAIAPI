using System.Net.Http.Json;
using SimpleAIAPI.Models;

namespace SimpleAIAPI.Services;

public interface IOllamaService
{
    Task<string> ChatAsync(string model, List<OllamaMessage> messages);
}

public class OllamaService(HttpClient httpClient, IConfiguration configuration) : IOllamaService
{
    public async Task<string> ChatAsync(string model, List<OllamaMessage> messages)
    {
        var ollamaIp = configuration["OLLAMA_IP"] ?? "localhost:11434";
        var url = $"http://{ollamaIp}/api/chat";

        var requestBody = new OllamaChatRequest(model, messages, false);
        var response = await httpClient.PostAsJsonAsync(url, requestBody, OllamaJsonSerializerContext.Default.OllamaChatRequest);

        if (!response.IsSuccessStatusCode)
        {
            return $"Error: {response.StatusCode}";
        }

        var result = await response.Content.ReadFromJsonAsync(OllamaJsonSerializerContext.Default.OllamaChatResponse);
        return result?.Message.Content ?? "No response from AI";
    }
}
