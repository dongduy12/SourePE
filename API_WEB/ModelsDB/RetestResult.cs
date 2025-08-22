namespace API_WEB.ModelsDB
{
    public class RetestResult
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
