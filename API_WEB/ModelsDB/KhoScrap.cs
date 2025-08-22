using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("KhoScrap")]
    public class KhoScrap
    {
        [Key]
        public string SERIAL_NUMBER { get; set; } = null!;
        public string? ShelfCode { get; set; }
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        public int? Position { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? entryDate { get; set; }
        public string? entryPerson { get; set; }
        public string? Note { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? borrowDate { get; set; }
        public string? borrowPerson { get; set; }
        public string? borrowStatus { get; set; }
    }
}
