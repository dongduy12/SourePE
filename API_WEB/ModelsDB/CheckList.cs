using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YourNamespace.Models
{
    [Table("CheckList")]
    public class CheckList
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // Không để IDENTITY vì ID là string
        [StringLength(50)]
        public string? ID { get; set; }

        [Required]
        [StringLength(50)]
        public string? Publisher { get; set; }

        public DateTime Time { get; set; }

        [StringLength(50)]
        public string? Project { get; set; }

        [StringLength(50)]
        public string? ModelName { get; set; }

        [StringLength(50)]
        public string? Station { get; set; }

        [StringLength(50)]
        public string? Error { get; set; }

        public int Input { get; set; }

        public int NGQty { get; set; }

        [StringLength(50)]
        public string? DFR { get; set; }

        [StringLength(50)]
        public string? RootCause { get; set; }

        [StringLength(50)]
        public string? Location { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string? FA { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string? Action { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [StringLength(50)]
        public string? Owner { get; set; }

        [Required]
        [StringLength(255)]
        public string? LinkPicture { get; set; }
    }
}
