using System;
namespace API_WEB.ModelsDB
{
    public class ScanLog
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
