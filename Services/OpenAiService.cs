using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace project_celestial_shield.Services
{
    public class OpenAiService
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };
        private readonly ILogger<OpenAiService>? _logger;

        public OpenAiService(HttpClient http, ILogger<OpenAiService>? logger = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger;
        }

        /// <summary>
        /// Request a chat completion and try to return a structured result of type T.
        /// This supports function-calling style responses (function_call.arguments) and falls back
        /// to extracting the first JSON object from assistant content when necessary.
        /// </summary>
        public async Task<T?> GetStructuredResultAsync<T>(string systemPrompt, string userPrompt, object? functionSchema = null, string? model = null, string? deploymentId = null)
            where T : class
        {
            var requestBody = new
            {
                model = model,
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                functions = functionSchema == null ? null : new object[] { functionSchema },
                function_call = functionSchema == null ? null : "auto",
                temperature = 0.0
            };

            string requestJson = JsonSerializer.Serialize(requestBody, _jsonOptions);

            _logger?.LogInformation("OpenAiService: sending request (model={Model}, deploymentId={DeploymentId})", model, deploymentId);
            _logger?.LogDebug("OpenAiService: request JSON: {RequestJson}", requestJson);

            using var req = new HttpRequestMessage(HttpMethod.Post, BuildCompletionsPath(deploymentId));
            req.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            _logger?.LogInformation("OpenAiService: response status {StatusCode}", res.StatusCode);
            res.EnsureSuccessStatusCode();

            var responseString = await res.Content.ReadAsStringAsync();
            _logger?.LogDebug("OpenAiService: response body: {ResponseBody}", responseString);

            using var doc = JsonDocument.Parse(responseString);

            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var first = choices[0];

                if (first.TryGetProperty("message", out var message)
                    && message.TryGetProperty("function_call", out var functionCall)
                    && functionCall.TryGetProperty("arguments", out var argsElement))
                {
                    if (argsElement.ValueKind == JsonValueKind.String)
                    {
                        var raw = argsElement.GetString() ?? "{}";
                        return TryDeserialize<T>(raw);
                    }
                    else
                    {
                        var rawText = argsElement.GetRawText();
                        return TryDeserialize<T>(rawText);
                    }
                }

                if (first.TryGetProperty("message", out message) && message.TryGetProperty("content", out var contentElement))
                {
                    string content = contentElement.GetString() ?? string.Empty;
                    var jsonText = ExtractJsonFromText(content);
                    if (!string.IsNullOrEmpty(jsonText))
                        return TryDeserialize<T>(jsonText);
                }
            }

            return null;
        }

        private string BuildCompletionsPath(string? deploymentId)
        {
            if (!string.IsNullOrEmpty(deploymentId))
                return $"/openai/deployments/{deploymentId}/chat/completions?api-version=2023-10-01-preview";

            return "/v1/chat/completions";
        }

        private static T? TryDeserialize<T>(string json) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractJsonFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                return text[start..(end + 1)];
            return null;
        }
    }
}
