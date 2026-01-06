using HtmlAgilityPack;
using AiNewsPulse.Core.Entities;
using AiNewsPulse.Core.Interfaces;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;

namespace AiNewsPulse.Infrastructure.Services
{
    public class Scraper : INewsScraper
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public Scraper(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<NewsItem>> GetNewsAsync(string url)
        {
            var newsList = new List<NewsItem>();
            try
            {
                var client = _httpClientFactory.CreateClient("ScraperClient");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var responseBytes = await client.GetByteArrayAsync(url);

                var htmlContent = Encoding.UTF8.GetString(responseBytes);

                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var nodes = doc.DocumentNode.SelectNodes("//h1/a | //h2/a | //h3/a | //article//a | //div[contains(@class,'title')]/a");

                if (nodes == null) return newsList;

                foreach (var node in nodes)
                {
                    var title = node.InnerText.Trim();
                    var link = node.GetAttributeValue("href", "");

                    if (string.IsNullOrEmpty(title) || title.Length < 10 || string.IsNullOrEmpty(link)) continue;


                    title = WebUtility.HtmlDecode(title);

                    title = title.Replace("÷", "'")
                                 .Replace("&#8217;", "'")
                                 .Replace("’", "'")
                                 .Replace("`", "'");

                    title = Regex.Replace(title, @"\s+", " ");

                    if (!link.StartsWith("http"))
                    {
                        var uri = new Uri(url);
                        if (!link.StartsWith("/")) link = "/" + link;
                        link = $"{uri.Scheme}://{uri.Host}{link}";
                    }

                    if (newsList.Any(n => n.Url == link)) continue;

                    var newsItem = new NewsItem
                    {
                        Title = title,
                        Url = link,
                        Source = new Uri(url).Host,
                        PublishDate = DateTime.Now,
                        RawContent = title
                    };

                    try { newsItem.RawContent = await GetArticleContent(client, link); } catch { }

                    newsList.Add(newsItem);
                    if (newsList.Count >= 3) break;
                }
            }
            catch { }
            return newsList;
        }

        private async Task<string> GetArticleContent(HttpClient client, string articleUrl)
        {
            try
            {
                var responseBytes = await client.GetByteArrayAsync(articleUrl);
                var html = Encoding.UTF8.GetString(responseBytes);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var sb = new StringBuilder();
                var paragraphs = doc.DocumentNode.SelectNodes("//p");

                if (paragraphs != null)
                {
                    foreach (var p in paragraphs)
                    {
                        var text = WebUtility.HtmlDecode(p.InnerText.Trim());
                        text = text.Replace("÷", "'").Replace("&#8217;", "'").Replace("’", "'");

                        if (text.Length > 30) sb.AppendLine(text);
                    }
                }
                return sb.ToString();
            }
            catch { return ""; }
        }
    }
}