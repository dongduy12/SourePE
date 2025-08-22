using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("ScrapList")] // Đặt tên bảng tương ứng trong SQL Server
    public class ScrapList
    {
        [Key] // Định nghĩa SN là khóa chính
        [Column("SN")]
        [Required] // Đảm bảo SN không được NULL vì là khóa chính
        [StringLength(50)]
        public string SN { get; set; } = null!; // nvarchar(50), không cho phép NULL

        [Column("KanBanStatus")]
        [Required]
        [StringLength(50)]
        public string KanBanStatus { get; set; } = null!; // nvarchar(50), không cho phép NULL

        [Column("Sloc")]
        [Required] // Không cho phép NULL
        [StringLength(50)]
        public string Sloc { get; set; } = null!; // nvarchar(50), không cho phép NULL

        [Column("TaskNumber")]
        [StringLength(50)]
        public string? TaskNumber { get; set; } = null!; // nvarchar(50),  cho phép NULL

        [Column("PO")]
        [StringLength(50)]
        public string? PO { get; set; } // nvarchar(50), cho phép NULL

        [Column("CreateBy")]
        [Required]
        [StringLength(50)]
        public string CreatedBy { get; set; } = null!; // nvarchar(50), Khong cho phép NULL

        [Column("Cost")]
        [Required]
        [StringLength(50)]
        public string Cost { get; set; } = null!; // nvarchar(50), Khong cho phép NULL

        [Column("InternalTask")]
        [Required]
        [StringLength(50)]
        public string InternalTask { get; set; } = null!; // nvarchar(50), Khong cho phép NULL

        [Column("Description")]
        [Required]
        [StringLength(50)]
        public string Desc { get; set; } = null!; // nvarchar(50), Khong cho phép NULL

        [Column("CreateTime")]
        [Required]
        public DateTime CreateTime { get; set; } // datetime, Khong cho phép NULL

        [Column("ApproveScrapPerson")]
        [Required]
        [StringLength(50)]
        public string ApproveScrapperson { get; set; } = null!; // nvarchar(50), Khong cho phép NULL

        [Column("ApplyTaskStatus")]
        [Required]
        public int ApplyTaskStatus { get; set; } // int, Khong cho phép NULL

        [Column("FindBoardStatus")]
        [Required]
        [StringLength(50)]
        public string FindBoardStatus { get; set; } = null!; // nvarchar(50), Khong cho phép NULL

        [Column("Remark")]
        [StringLength(50)]
        public string? Remark { get; set; } // nvarchar(50), cho phép NULL

        [Column("Purpose")]
        [Required] // Không cho phép NULL
        [StringLength(50)]
        public string Purpose { get; set; } = null!; // nvarchar(50), không cho phép NULL

        [Column("Category")]
        [Required] // Không cho phép NULL
        [StringLength(50)]
        public string Category { get; set; } = null!; // nvarchar(50), không cho phép NULL

        [Column("ApplyTime")]
        public DateTime? ApplyTime { get; set; } // datetime, cho phép NULL
    }
}