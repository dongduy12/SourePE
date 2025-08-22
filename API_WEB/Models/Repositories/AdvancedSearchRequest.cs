namespace API_WEB.Models.Repositories
{
    public class AdvancedSearchRequest
    {
        public List<string> WIPGroup { get; set; }
        public string TestCode { get; set; }
        public string Data1 { get; set; }
        public List<string> Sfg { get; set; }
        public string BorrowStatus { get; set; }
    }
}
