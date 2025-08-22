using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.Models
{
    public partial class PdStock
    {
        public string? SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? CartonNo { get; set; }
        public string? LocationStock { get; set; }
        public DateTime? EntryDate { get; set; }
        public string? EntryOp { get; set; }
    }
}
