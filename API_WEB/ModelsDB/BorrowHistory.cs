using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.ModelsDB
{
    [Table("BorrowHistory")]
    public partial class BorrowHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        [StringLength(50)]
        public string SerialNumber { get; set; } = null!;

        [StringLength(200)]
        public string? BorrowPerson { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? BorrowDate { get; set; }

        [Required]
        [StringLength(50)]
        public string? ProductLine { get; set; }

        [Required]
        [StringLength(50)]
        public string? ModelName { get; set; }
    }
}
