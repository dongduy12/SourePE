
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("CheckInGuides")]
    public class CheckInGuides
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }
        public string? Guide { get; set; }
        public string? ProductLine { get; set; }
        public string? ErrorCode { get; set; }
    }
}