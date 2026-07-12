using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Sprint 7: AI-powered lyrics translation.
    ///
    /// Supports multiple providers via the OpenAI-compatible chat completions API:
    ///   - OpenRouter (https://openrouter.ai/api/v1/chat/completions)
    ///   - OpenAI     (https://api.openai.com/v1/chat/completions)
    ///   - Gemini     (https://generativelanguage.googleapis.com/v1beta/openai/chat/completions)
    ///   - Groq       (https://api.groq.com/openai/v1/chat/completions)
    ///   - Mistral    (https://api.mistral.ai/v1/chat/completions)
    ///   - xAI        (https://api.x.ai/v1/chat/completions)
    ///   - Custom     (user provides base URL)
    ///
    /// All providers use the same request format (OpenAI-compatible), so we
    /// just need to swap the base URL and API key.
    /// </summary>
    public class AiTranslationService
    {
        private static readonly HttpClient _client;

        static AiTranslationService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Maps provider names to their default base URLs.
        /// </summary>
        public static readonly Dictionary<string, string> ProviderUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "OpenRouter", "https://openrouter.ai/api/v1/chat/completions" },
            { "OpenAI", "https://api.openai.com/v1/chat/completions" },
            { "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" },
            { "Groq", "https://api.groq.com/openai/v1/chat/completions" },
            { "Mistral", "https://api.mistral.ai/v1/chat/completions" },
            { "xAI", "https://api.x.ai/v1/chat/completions" },
            { "Perplexity", "https://api.perplexity.ai/chat/completions" },
            { "Nvidia", "https://integrate.api.nvidia.com/v1/chat/completions" },
        };

        /// <summary>
        /// Translates lyrics line-by-line into the target language.
        /// Returns the translated lyrics (same line count as input).
        /// </summary>
        public async Task<string> TranslateLyricsAsync(string lyrics, string targetLanguage,
            string provider, string apiKey, string model)
        {
            if (string.IsNullOrWhiteSpace(lyrics))
                return null;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("AI API key is required.");
            if (string.IsNullOrEmpty(model))
                throw new InvalidOperationException("AI model is required.");

            var baseUrl = ProviderUrls.TryGetValue(provider ?? "OpenRouter", out var url)
                ? url
                : provider; // If not found, assume the provider field IS the URL (custom)

            var lines = lyrics.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lineCount = lines.Length;

            var systemPrompt = "You are a precise lyrics translation assistant. " +
                "Your output must ALWAYS be a valid JSON array of strings. " +
                $"Translate each line into {targetLanguage}. " +
                $"Return EXACTLY {lineCount} items. " +
                "Preserve empty lines as empty strings. " +
                "Output ONLY the JSON array, no explanations.";

            var userPrompt = $"Translate these {lineCount} lines into {targetLanguage}:\n\n" +
                string.Join("\n", lines);

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = Math.Max(1000, lineCount * 50)
            };

            var requestJson = JsonConvert.SerializeObject(requestBody);

            using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl))
            {
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using (var response = await _client.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        throw new InvalidOperationException(
                            $"AI API returned {response.StatusCode}: {errorBody}");
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    var parsed = JObject.Parse(responseJson);

                    var content = parsed["choices"]?[0]?["message"]?["content"]?.Value<string>();
                    if (string.IsNullOrEmpty(content))
                        throw new InvalidOperationException("AI returned empty response.");

                    // The content should be a JSON array of strings
                    content = content.Trim();
                    if (content.StartsWith("```"))
                    {
                        // Strip markdown code fences
                        content = content.Replace("```json", "").Replace("```", "").Trim();
                    }

                    var translatedLines = JsonConvert.DeserializeObject<List<string>>(content);
                    if (translatedLines == null || translatedLines.Count != lineCount)
                    {
                        // Fallback: if the AI didn't return the right count,
                        // join whatever we got
                        if (translatedLines != null && translatedLines.Count > 0)
                            return string.Join("\n", translatedLines);
                        return null;
                    }

                    return string.Join("\n", translatedLines);
                }
            }
        }

        /// <summary>
        /// Returns true if AI translation is enabled and configured.
        /// </summary>
        public bool IsConfigured()
        {
            try
            {
                return Musicefy.Properties.Settings.Default.AiTranslationEnabled
                    && !string.IsNullOrEmpty(Musicefy.Properties.Settings.Default.AiTranslationApiKey);
            }
            catch { return false; }
        }
    }
}
