using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.ModelsDB
{
    [Table("Export")]
    public partial class Export
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // ID sẽ tự động tăng
        [Column("ID")]
        public int Id { get; set; }
        [Column("SERIAL_NUMBER")]
        [StringLength(100)]
        public string SerialNumber { get; set; } = null!;
        [Column(TypeName = "datetime")]
        public DateTime? ExportDate { get; set; }
        [StringLength(100)]
        public string? ExportPerson { get; set; }
        [StringLength(100)]
        public string? ProductLine { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? EntryDate { get; set; }
        [StringLength(100)]
        public string? EntryPerson { get; set; }
        [StringLength(100)]
        public string? ModelName { get; set; }

        [Column("CHECKING_B36R")]
        public int? CheckingB36R { get; set; }

        [Column("LINK_TIME", TypeName = "datetime")]
        public DateTime? LinkTime { get; set; }

        [Column("KANBAN_TIME", TypeName = "datetime")]
        public DateTime? KanbanTime { get; set; }
    }
}
