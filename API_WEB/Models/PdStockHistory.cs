using System;
using System.Collections.Generic;

namespace API_WEB.Models
{
    public partial class PdStockHistory
    {
        public int Id { get; set; }
        public string? SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? CartonNo { get; set; }
        public string? LocationStock { get; set; }
        public DateTime? EntryDate { get; set; }
        public string? EntryOp { get; set; }
        public DateTime? OutDate { get; set; }
        public string? OutOp { get; set; }
    }
}
