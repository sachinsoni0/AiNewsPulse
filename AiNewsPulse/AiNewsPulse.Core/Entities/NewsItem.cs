namespace AiNewsPulse.Core.Entities
{
    public class NewsItem
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime PublishDate { get; set; }
        public string RawContent { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TitleTR { get; set; } = string.Empty;
        public string SummaryTR { get; set; } = "Özet Yok";
        public string TitleEN { get; set; } = string.Empty;
        public string SummaryEN { get; set; } = "No Summary";
        public string Category { get; set; } = "General";
        public string Sentiment { get; set; } = "Neutral";
    }
}