using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("Logs")]
    public class LogKhoScrap
    {
        [Key]
        public int Id { get; set; }
        public string Action { get; set; } = null!;
        public string SerialNumber { get; set; } = null!;
        public string User { get; set; } = null!;
        public string? Note { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
