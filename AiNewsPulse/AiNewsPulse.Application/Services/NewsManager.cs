using System.Diagnostics;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AiNewsPulse.Core.Entities;
using AiNewsPulse.Core.Interfaces;
using AiNewsPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNewsPulse.Application.Services
{
    public class NewsManager
    {
        private readonly INewsScraper _scraper;
        private readonly IAiService _aiService;
        private readonly AppDbContext _context;

        public NewsManager(INewsScraper scraper, IAiService aiService, AppDbContext context)
        {
            _scraper = scraper;
            _aiService = aiService;
            _context = context;
        }

        public async Task AddSourceAsync(string url)
        {
            if (await _context.NewsSources.AnyAsync(s => s.Url == url)) { Console.WriteLine($"[INFO] Already exists."); return; }
            _context.NewsSources.Add(new NewsSource { Url = url, IsActive = true });
            await _context.SaveChangesAsync();
            Console.WriteLine($"[SUCCESS] Added.");
        }

        public async Task RemoveSourceAsync(int id)
        {
            var s = await _context.NewsSources.FindAsync(id);
            if (s != null) { _context.NewsSources.Remove(s); await _context.SaveChangesAsync(); Console.WriteLine($"[SUCCESS] Removed."); }
        }

        public async Task ListSourcesAsync()
        {
            var sources = await _context.NewsSources.ToListAsync();
            Console.WriteLine("\n--- ACTIVE SOURCES ---");
            foreach (var s in sources) Console.WriteLine($"ID: {s.Id} | {s.Url}");
            Console.WriteLine("----------------------\n");
        }

        public async Task RunDailyBriefingAsync()
        {
            var sources = await _context.NewsSources.Where(s => s.IsActive).ToListAsync();
            if (sources.Count == 0) { Console.WriteLine("[WARNING] No sources found."); return; }

            var processedNews = new List<NewsItem>();

            foreach (var source in sources)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[SCANNING] {source.Url} ... ");
                Console.ResetColor();

                try
                {
                    var newsFromSite = await _scraper.GetNewsAsync(source.Url);

                    if (newsFromSite.Count == 0)
                    {
                        Console.WriteLine("No news.");
                        continue;
                    }

                    Console.WriteLine("DONE."); 

                    foreach (var item in newsFromSite)
                    {
                        try
                        {
                            bool exists = await _context.News.AnyAsync(n => n.Url == item.Url);
                            if (exists)
                            {
                                Console.WriteLine($"   -> [SKIPPED] Exists.");
                                processedNews.Add(await _context.News.FirstAsync(n => n.Url == item.Url));
                                continue;
                            }

                            Console.Write($"   -> [ANALYZING] New Article ... ");
                            var result = await _aiService.ProcessNewsAsync(item);
                            _context.News.Add(result);
                            await _context.SaveChangesAsync();
                            processedNews.Add(result);
                            Console.WriteLine("SAVED.");
                        }
                        catch { Console.WriteLine("ERROR."); }
                    }
                }
                catch { Console.WriteLine("FAILED."); }
            }

            GenerateAndOpenHtml(processedNews);
        }

        private void GenerateAndOpenHtml(List<NewsItem> newsList)
        {
            string filePath = Path.Combine(Environment.CurrentDirectory, "DailyReport.html");
            var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), WriteIndented = true };

            var jsData = newsList.Select(n => new
            {
                source = n.Source,
                category = n.Category,
                sentiment = n.Sentiment,
                title_tr = n.TitleTR,
                title_en = n.TitleEN,
                summary_tr = n.SummaryTR,
                summary_en = n.SummaryEN,
                url = n.Url
            }).ToList();

            string jsonString = JsonSerializer.Serialize(jsData, jsonOptions);

            string html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>AI News Pulse</title>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background-color: #121212; color: #e0e0e0; margin: 0; padding: 20px; }}
        .header {{ display: flex; justify-content: space-between; align-items: center; background: #1f1f1f; padding: 20px 30px; border-radius: 12px; border: 1px solid #333; }}
        .header h2 {{ margin: 0; color: #bb86fc; }}
        .btn {{ background: #03dac6; color: #000; border: none; padding: 10px 20px; cursor: pointer; font-weight: bold; border-radius: 6px; }}
        .container {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; margin-top: 25px; }}
        .card {{ background: #1e1e1e; padding: 20px; border-radius: 12px; border: 1px solid #333; display: flex; flex-direction: column; }}
        .card:hover {{ transform: translateY(-5px); border-color: #bb86fc; }}
        .tag {{ font-size: 11px; padding: 4px 10px; border-radius: 20px; color: white; display: inline-block; margin-bottom: 12px; font-weight: bold; margin-right: 5px; }}
        .tag.cat {{ background: #3700b3; }}
        .tag.pos {{ background: #00695c; color: #80cbc4; }}
        .tag.neg {{ background: #b00020; color: #ef9a9a; }}
        .tag.neu {{ background: #424242; color: #bdbdbd; }}
        h3 {{ margin: 0 0 10px 0; color: #ffffff; font-size: 18px; }}
        p {{ color: #b0b0b0; font-size: 14px; flex-grow: 1; }}
        a {{ color: #bb86fc; text-decoration: none; font-size: 13px; font-weight: bold; margin-top: 10px; }}
        .source-name {{ font-size: 11px; color: #666; margin-bottom: 5px; display: block; }}
    </style>
</head>
<body>
<div class='header'>
    <h2 id='pageTitle'>AI News Pulse</h2>
    <button id='langBtn' class='btn' onclick='toggleLanguage()'>🇹🇷 Türkçe</button>
</div>
<div class='container' id='newsContainer'></div>
<script>
    const newsData = {jsonString}; 
    let currentLang = 'en';

    // GENİŞLETİLMİŞ SÖZLÜK
    const dictionary = {{
        'Technology': 'Teknoloji', 'Economy': 'Ekonomi', 'Health': 'Sağlık', 'Politics': 'Siyaset', 
        'Sports': 'Spor', 'General': 'Genel', 'Science': 'Bilim', 'Automotive': 'Otomotiv',
        'Business': 'İş Dünyası', 'Mobile': 'Mobil', 'Software': 'Yazılım', 'Hardware': 'Donanım', 'Game': 'Oyun', 'Gaming': 'Oyun',
        'Artificial Intelligence': 'Yapay Zeka', 'AI': 'Yapay Zeka', 'Development': 'Geliştirme', 'Internet': 'İnternet',
        'Space': 'Uzay', 'Review': 'İnceleme', 'Promotions': 'Kampanya', 'Deals': 'Fırsatlar',
        'Positive': 'Pozitif', 'Negative': 'Negatif', 'Neutral': 'Nötr'
    }};

    function translate(word) {{
        if (currentLang === 'en' || !word) return word;
        const key = word.trim();
        return dictionary[key] || word; 
    }}

    function renderNews() {{
        const container = document.getElementById('newsContainer');
        container.innerHTML = '';
        
        document.getElementById('pageTitle').innerText = currentLang === 'en' ? 'AI News Pulse - Daily Briefing' : 'AI News Pulse - Günlük Bülten';
        document.getElementById('langBtn').innerText = currentLang === 'en' ? '🇹🇷 Türkçe' : '🇬🇧 English';
        
        if (!newsData || newsData.length === 0) {{
            container.innerHTML = '<p style=""text-align:center; width:100%; color:#666;"">No news found.</p>';
            return;
        }}

        newsData.forEach(item => {{
            const title = currentLang === 'en' ? (item.title_en || item.title_tr) : item.title_tr;
            const summary = currentLang === 'en' ? (item.summary_en || item.summary_tr) : item.summary_tr;
            
            const categoryDisplay = translate(item.category);
            const sentimentDisplay = translate(item.sentiment);

            let sentClass = 'neu';
            if (item.sentiment && item.sentiment.toLowerCase().includes('pos')) sentClass = 'pos';
            else if (item.sentiment && item.sentiment.toLowerCase().includes('neg')) sentClass = 'neg';

            container.innerHTML += `
                <div class='card'>
                    <span class='source-name'>${{item.source}}</span>
                    <div>
                        <span class='tag cat'>${{categoryDisplay}}</span>
                        <span class='tag ${{sentClass}}'>${{sentimentDisplay}}</span>
                    </div>
                    <h3>${{title}}</h3>
                    <p>${{summary}}</p>
                    <a href='${{item.url}}' target='_blank'>${{currentLang === 'en' ? 'Read More →' : 'Devamını Oku →'}}</a>
                </div>
            `;
        }});
    }}

    function toggleLanguage() {{ 
        currentLang = currentLang === 'en' ? 'tr' : 'en'; 
        renderNews(); 
    }}
    
    renderNews();
</script>
</body>
</html>";

            File.WriteAllText(filePath, html);
            Console.WriteLine($"[REPORT] Generated: DailyReport.html");
            try { new Process { StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true } }.Start(); } catch { }
        }
    }
}