using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("ScanLog")]
    public class ScanLog
    {
        [Key]
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
