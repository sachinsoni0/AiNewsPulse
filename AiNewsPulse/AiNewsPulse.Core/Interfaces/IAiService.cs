using System.Threading.Tasks;
using AiNewsPulse.Core.Entities;

namespace AiNewsPulse.Core.Interfaces
{
    public interface IAiService
    {
        Task<NewsItem> ProcessNewsAsync(NewsItem newsItem);
    }
}