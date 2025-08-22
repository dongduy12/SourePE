namespace API_WEB.Models.SmartFA
{
    // Model kết hợp để chứa tất cả các cột
    public class CombinedDataModel
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductLine { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ShelfCode { get; set; } = string.Empty;
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        public int? PositionInTray { get; set; }
        public string KanBanWIP { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public string Data1 { get; set; } = string.Empty;
        public string WipGroup { get; set; } = string.Empty;
        public string TestGroup { get; set; } = string.Empty;
        public string MoNumber { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public DateTime? EntryDate { get; set; }
        public string EntryPerson { get; set; } = string.Empty;
        public string BorrowStatus { get; set; } = string.Empty;
        public DateTime? BorrowDate { get; set; }
        public string BorrowPerson { get; set; }
        public string Note { get; set; } = string.Empty;
        public string ActionNe { get; set; } = string.Empty;
        public string Scrap { get; set; } = string.Empty;
        public string Data11 { get; set; } = string.Empty;
        public string Data12 { get; set; } = string.Empty;
        public DateTime? Date3 { get; set; }
        public string Tester { get; set; } = string.Empty;
        public string Data13 { get; set; } = string.Empty;
        public string Data17 { get; set; } = string.Empty;
        public string Data18 { get; set; } = string.Empty;
        public string HoldReason { get; set; } = string.Empty;
        public string BlockReason { get; set; } = string.Empty;
    }
}
