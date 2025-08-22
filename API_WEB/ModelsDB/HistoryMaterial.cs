using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    public class HistoryMaterial
    {
        [Key] // Đánh dấu cột ID là khóa chính
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Tự động tăng (IDENTITY)
        public int ID { get; set; }

        [Required] // NOT NULL
        [StringLength(50)] // Độ dài tối đa 50
        public string TASK { get; set; }

        [Required] // NOT NULL
        [StringLength(50)]
        public string MA_LIEU { get; set; }

        [StringLength(50)]
        public string? MA_ALL { get; set; } // Cho phép NULL

        [Required] // NOT NULL
        [StringLength(100)]
        public string NHA_CUNG_UNG { get; set; }

        [Required] // NOT NULL
        [StringLength(50)]
        public string DATE_CODE { get; set; }

        [Required] // NOT NULL
        [StringLength(50)]
        public string LOT_CODE { get; set; }

        [Required] // NOT NULL
        [StringLength(50)]
        public string TYPE { get; set; }

        public int? QTY1 { get; set; } // Cho phép NULL

        public int? QTY2 { get; set; } // Cho phép NULL

        [Required] // NOT NULL
        [StringLength(50)]
        public string TIME1 { get; set; }

        [Required] // NOT NULL
        [StringLength(50)]
        public string OP1 { get; set; }

        [StringLength(50)]
        public string? TIME2 { get; set; } // Cho phép NULL

        [StringLength(50)]
        public string? OP2 { get; set; } // Cho phép NULL

        [Required] // NOT NULL
        [StringLength(50)]
        public string ESD { get; set; }

        [StringLength(50)]
        public string? LOCATION { get; set; } // Cho phép NULL

        [StringLength(200)]
        public string? REMARK { get; set; } // Cho phép NULL

        public int? QTY3 { get; set; } // Cho phép NULL

        public int? QTY4 { get; set; } // Cho phép NULL
    }
}