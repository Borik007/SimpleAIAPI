using System.Net.Http.Json;
using SimpleAIAPI.Models;

namespace SimpleAIAPI.Services;

public interface IOllamaService
{
    Task<string> ChatAsync(string model, List<OllamaMessage> messages);
}

public class OllamaService(HttpClient httpClient) : IOllamaService
{
    public async Task<string> ChatAsync(string model, List<OllamaMessage> messages)
    {
        const string url = "api/chat";

        var requestBody = new OllamaChatRequest(model, messages, false);
        var response = await httpClient.PostAsJsonAsync(url, requestBody, OllamaJsonSerializerContext.Default.OllamaChatRequest);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return $"Error: {response.StatusCode} - {errorContent}";
        }

        var result = await response.Content.ReadFromJsonAsync(OllamaJsonSerializerContext.Default.OllamaChatResponse);
        return result?.Message.Content ?? "No response from AI";
    }
}
