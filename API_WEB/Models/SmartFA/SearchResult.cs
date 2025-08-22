namespace API_WEB.Models.SmartFA
{
    public class SearchResult
    {
        public string? SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? MONumber { get; set; }
        public string? TestGroup { get; set; }
        public string? TestCode { get; set; }
        public string? Data1 { get; set; }
        public string? Data11 { get; set; }
        public string? Data12 { get; set; }
        public DateTime? Date3 { get; set; }
        public string? Tester { get; set; }
        public string? Data13 { get; set; }
        public string? Data17 { get; set; }
        public string? Data18 { get; set; }
        public string? WipGroup { get; set; }
        public string? ProductLine { get; set; }
        public string? ShelfCode { get; set; }
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        public int? PositionInTray { get; set; }
        public string? BorrowStatus { get; set; }
        public string? KanBanWIP { get; set; }
        public string? ScrapStatus { get; set; }
    }
}
