using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.ModelsDB
{
    [Table("RepairHistory")]
    public class RepairHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }
        public string ?SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? ProductLine { get; set; }
        public DateTime? RepairTime { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<RepairAction>? RepairActions { get; set; } //Tạo mối quan hệ 1-n giữa RepairHistory và RepairActions
    }
}

