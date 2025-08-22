using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{

    [Table("HassBi")] // Đặt tên bảng tương ứng trong SQL Server
    public class HassBi
    {
        [Key]
        public int ID { get; set; } // Key chính

        [Required]
        [StringLength(50)] // Giới hạn độ dài cho chuỗi
        public string? MoNumber { get; set; } 

        [Required]
        [StringLength(50)]
        public string? Type { get; set; } 

        public DateTime TimeStart { get; set; } // Lưu trữ thời gian bắt đầu

        [Required]
        [StringLength(50)]
        public string? Status { get; set; }  // Trạng thái

        [StringLength(250)]
        public string? Remark { get; set; }  // Ghi chú, không bắt buộc

        [StringLength(50)]
        public string? NvConfirm { get; set; }  // Người xác nhận

        public DateTime? ConfirmTime { get; set; } // Thời gian xác nhận (có thể null) 
    }
}
