namespace AiNewsPulse.Core.Entities
{
    public class NewsSource
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}