using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AiNewsPulse.Core.Interfaces;
using AiNewsPulse.Infrastructure.Services;
using AiNewsPulse.Application.Services;
using AiNewsPulse.Infrastructure.Data;
using AiNewsPulse.Core.Entities;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("ScraperClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = true,
        AllowAutoRedirect = true
    });

builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddSingleton<INewsScraper, Scraper>();
builder.Services.AddSingleton<IAiService, OllamaService>();
builder.Services.AddScoped<NewsManager>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.NewsSources.Any())
    {
        db.NewsSources.AddRange(
            new NewsSource { Url = "https://shiftdelete.net/", IsActive = true },
            new NewsSource { Url = "https://webrazzi.com/", IsActive = true },
            new NewsSource { Url = "https://www.donanimhaber.com/", IsActive = true }
        );
        db.SaveChanges();
    }
}

bool autoStart = false; 

while (true)
{
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("=================================================");
    Console.WriteLine("       AI NEWS PULSE - MANAGEMENT CONSOLE        ");
    Console.WriteLine("=================================================");
    Console.ResetColor();
    Console.WriteLine("[1] START SCANNING (Taramayı Başlat ve Raporla)");
    Console.WriteLine("[2] LIST SOURCES   (Kaynakları Listele)");
    Console.WriteLine("[3] ADD SOURCE     (Yeni Kaynak Ekle)");
    Console.WriteLine("[4] REMOVE SOURCE  (Kaynak Sil)");
    Console.WriteLine("[5] EXIT           (Çıkış)");
    Console.WriteLine("-------------------------------------------------");

    string input;
    if (autoStart)
    {
        input = "1"; 
        autoStart = false; 
    }
    else
    {
        Console.Write("Select / Seçim: ");
        input = Console.ReadLine();
    }

    using (var scope = host.Services.CreateScope())
    {
        var manager = scope.ServiceProvider.GetRequiredService<NewsManager>();
        switch (input)
        {
            case "1":
                await manager.RunDailyBriefingAsync();
                Pause();
                break;
            case "2":
                await manager.ListSourcesAsync();
                Pause();
                break;
            case "3":
                Console.Write("URL: ");
                var u = Console.ReadLine();
                if (!string.IsNullOrEmpty(u))
                {
                    await manager.AddSourceAsync(u);
                    Console.WriteLine("Starting scan automatically... / Otomatik tarama başlıyor...");
                    Thread.Sleep(1500);
                    autoStart = true; 
                }
                else Pause();
                break;
            case "4":
                await manager.ListSourcesAsync();
                Console.Write("ID: ");
                if (int.TryParse(Console.ReadLine(), out int i))
                {
                    await manager.RemoveSourceAsync(i);
                    Console.WriteLine("Refreshing report... / Rapor yenileniyor...");
                    Thread.Sleep(1500);
                    autoStart = true;
                }
                else Pause();
                break;
            case "5":
                Environment.Exit(0);
                break;
        }
    }
}

void Pause()
{
    Console.WriteLine("\nPress any key to return... / Menü için tuşa basın...");
    Console.ReadKey();
}