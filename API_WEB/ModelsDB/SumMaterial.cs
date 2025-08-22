using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    public class SumMaterial
    {
        [Key] // Đánh dấu cột ID là khóa chính
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Tự động tăng (IDENTITY)
        public int ID { get; set; }

        [Required] // NOT NULL
        [StringLength(50)]
        public string MA_LIEU { get; set; }

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
        public int TONG_LINH { get; set; }

        [Required] // NOT NULL
        public int SO_LUONG_OK { get; set; }

        [Required] // NOT NULL
        public int SO_LUONG_NG { get; set; }

        [Required] // NOT NULL
        public int CHO_MUON { get; set; }

        [Required] // NOT NULL
        public int DA_BAO_PHE { get; set; }

        [StringLength(50)]
        public string? VITRI_TRENBAN { get; set; } // Cho phép NULL

        [StringLength(50)]
        public string? MODEL { get; set; } // Cho phép NULL

        [Required] // NOT NULL
        [StringLength(50)]
        public string ESD { get; set; }

        [StringLength(50)]
        public string? LOCATION { get; set; } // Cho phép NULL

        [StringLength(200)]
        public string? REMARK { get; set; } // Cho phép NULL
    }
}