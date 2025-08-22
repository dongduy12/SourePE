using System;

namespace API_WEB.ModelsOracle
{
    public class FailedSerialNumber
    {
        public string? SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? MoNumber { get; set; }
        public string? GroupName { get; set; }
        public DateTime? FailTime { get; set; }
        public string? TestCode { get; set; }
        public string? Data1 { get; set; }

    }
}
