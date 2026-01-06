using Microsoft.EntityFrameworkCore;
using AiNewsPulse.Core.Entities;

namespace AiNewsPulse.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<NewsItem> News { get; set; }
        public DbSet<NewsSource> NewsSources { get; set; } 

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=haberler.db");
        }
    }
}