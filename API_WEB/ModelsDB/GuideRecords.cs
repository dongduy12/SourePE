using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("GuideRecords")]
    public class GuideRecords
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }
        public string? Check_Point { get; set; }
        public string? ProductLine { get; set; }
        public string? TestCode { get; set; }
    }
}
