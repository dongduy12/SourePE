using System;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.Models.SmartFA
{
    [Keyless]
    public class RepairOwnerBySerialResult
    {
        public string SerialNumber { get; set; }
        public string ModelName { get; set; }
        public string ReasonCode { get; set; }
        public string TestCode { get; set; }
        public string ErrorDesc { get; set; }
        public string? Repairer { get; set; }
        public string TestGroup { get; set; }
        public string? ErrorItemCode { get; set; }
        public string? RepairTime { get; set; }
    }
}
