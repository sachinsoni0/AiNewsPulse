using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNewsPulse.Core.Entities;
using AiNewsPulse.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AiNewsPulse.Application.Services
{
    public class OllamaService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelId;

        public OllamaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _modelId = configuration["AiSettings:ModelId"] ?? "llama3.2";
            _httpClient.Timeout = TimeSpan.FromMinutes(2); 
        }

        public async Task<NewsItem> ProcessNewsAsync(NewsItem newsItem)
        {
            var ollamaHost = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "localhost";
            var requestUrl = $"http://{ollamaHost}:11434/api/generate";

            var prompt = $@"
            You are a news analyst. Analyze the provided article and extract information.
            
            RULES:
            1. Output MUST be valid JSON.
            2. Do NOT use markdown code blocks.
            3. Translate and summarize correctly.
            4. Do NOT copy the field descriptions, replace them with actual content!

            REQUIRED JSON FORMAT:
            {{
                ""title_tr"": ""Write the Turkish title here"",
                ""title_en"": ""Write the English title here"",
                ""summary_tr"": ""Write a 2-sentence summary in Turkish here"",
                ""summary_en"": ""Write a 2-sentence summary in English here"",
                ""category"": ""One word category in English (e.g. Technology)"",
                ""sentiment"": ""Positive, Negative, or Neutral""
            }}

            ARTICLE CONTENT:
            {newsItem.RawContent}
            ";

            var payload = new
            {
                model = _modelId,
                prompt = prompt,
                stream = false,
                format = "json", 
                options = new { temperature = 0.1 } 
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(requestUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    return SetDefaults(newsItem, "Ollama Error");
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(responseString);
                var aiResponseText = jsonNode?["response"]?.ToString();

                if (!string.IsNullOrEmpty(aiResponseText))
                {
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var result = JsonSerializer.Deserialize<OllamaResponse>(aiResponseText, options);

                        if (result?.title_en != null && result.title_en.Contains("Write the"))
                        {
                            newsItem.SummaryTR = "AI Format Hatası (Tekrar Deneyin)";
                            newsItem.Sentiment = "Neutral";
                        }
                        else
                        {
                            newsItem.TitleTR = result?.title_tr ?? newsItem.Title;
                            newsItem.TitleEN = result?.title_en ?? newsItem.Title;
                            newsItem.SummaryTR = result?.summary_tr ?? "Özet çıkarılamadı.";
                            newsItem.SummaryEN = result?.summary_en ?? "No summary available.";
                            newsItem.Category = result?.category ?? "General";
                            newsItem.Sentiment = result?.sentiment ?? "Neutral";
                        }
                    }
                    catch
                    {
                        return SetDefaults(newsItem, "JSON Parse Error");
                    }
                }
            }
            catch (Exception ex)
            {
                return SetDefaults(newsItem, $"Connection Error: {ex.Message}");
            }

            return newsItem;
        }

        private NewsItem SetDefaults(NewsItem item, string error)
        {
            item.SummaryTR = error;
            item.SummaryEN = error;
            item.Category = "Error";
            item.Sentiment = "Neutral";
            return item;
        }
    }

    public class OllamaResponse
    {
        public string title_tr { get; set; }
        public string title_en { get; set; }
        public string summary_tr { get; set; }
        public string summary_en { get; set; }
        public string category { get; set; }
        public string sentiment { get; set; }
    }
}