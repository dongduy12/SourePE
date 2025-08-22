namespace API_WEB.Models.Repositories
{
    public class SerialNumberInfo
    {
        public string SerialNumber { get; set; } // Số serial
        public string ModelName { get; set; } // Tên model
        public DateTime TestTime { get; set; } // Thời gian kiểm tra
        public string TestCode { get; set; } // Mã kiểm tra
        public string Data1 { get; set; } // Dữ liệu 1
        public string ReasonCode { get; set; } // Mã lý do
        public string WipGroup { get; set; } // Nhóm WIP
    }
}
