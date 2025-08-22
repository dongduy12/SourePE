namespace API_WEB.Models.Repositories
{
    public class Export
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; }
        public DateTime? ExportDate { get; set; }
        public string? ExportPerson { get; set; }
        public string? ProductLine { get; set; }
        public DateTime? EntryDate { get; set; }
        public string? EntryPerson { get; set; }
        public string? ModelName { get; set; }
        public int? CheckingB36R { get; set; }
        public DateTime? LinkTime { get; set; }
    }
}
