using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("RepairActions")]
    public class RepairAction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Tự động tăng nếu là số nguyên
        [Column("Id")]
        public int Id { get; set; }

        [ForeignKey("RepairHistory")] // Tham chiếu đến bảng RepairHistory
        public int RepairHistoryId { get; set; }

        public string ?ActionDescription { get; set; }

        public DateTime? ActionTime { get; set; }

        public string ?ResponsiblePerson { get; set; }
        public string? Data1 { get; set; }
        public string? Note { get; set; }

        // Navigation Property
        public RepairHistory? RepairHistory { get; set; }
    }
}
