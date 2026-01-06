using System.Collections.Generic;
using System.Threading.Tasks;
using AiNewsPulse.Core.Entities;

namespace AiNewsPulse.Core.Interfaces
{
    public interface INewsScraper
    {
        Task<List<NewsItem>> GetNewsAsync(string url);
    }
}