using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.ModelsDB
{
    [Table("PdStockHistory")]
    public partial class PdStockHistory
    {
        [Column("SERIAL_NUMBER")]
        [StringLength(50)]
        public string? SerialNumber { get; set; }
        [Column("MODEL_NAME")]
        [StringLength(50)]
        public string? ModelName { get; set; }
        [Column("CARTON_NO")]
        [StringLength(50)]
        public string? CartonNo { get; set; }
        [Column("LOCATION_STOCK")]
        [StringLength(50)]
        public string? LocationStock { get; set; }
        [Column("ENTRY_DATE", TypeName = "datetime")]
        public DateTime? EntryDate { get; set; }
        [Column("ENTRY_OP")]
        [StringLength(50)]
        public string? EntryOp { get; set; }
        [Column("OUT_DATE", TypeName = "datetime")]
        public DateTime? OutDate { get; set; }
        [Column("OUT_OP")]
        [StringLength(50)]
        public string? OutOp { get; set; }

        [Key]
        [Column("Id")]
        public int Id { get; set; } // Khóa chính
    }
}
