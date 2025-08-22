namespace API_WEB.Models.SmartFA
{
    public class SearchRequestNe
    {
        public List<string>? SerialNumbers { get; set; } // Chấp nhận danh sách Serial Numbers
        public string? ModelName { get; set; }
        public string? TestCode { get; set; }
        public string? Status { get; set; } // DATA11
        public string? Data1 { get; set; }
        public string? HandoverStatus { get; set; }
        //public string? Location { get; set; }
    }
}
