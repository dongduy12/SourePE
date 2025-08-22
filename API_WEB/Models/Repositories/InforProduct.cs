namespace API_WEB.Models.Repositories
{
    public class InforProduct
    {
        public string SerialNumber { get; set; }
        public string ModelName { get; set; }
        public string MoNumber { get; set; }
        public string WipGroup { get; set; }
        public string WorkFlag { get; set; }
        public string TestGroup { get; set; }
        public string TestCode { get; set; }
        public string Data1 { get; set; }
        public string ReasonCode { get; set; }
        public string KanBanWIP { get; set; }
        public string ActionNe { get; set; }
        public string HoldReason { get; set; }
        public string BlockReason { get; set; }


        //====
        public string ProductLine { get; set; }
        public string ShelfCode { get; set; }
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        public int? PositionInTray { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? BorrowDate { get; set; }
        public string BorrowPerson { get; set; }
        public string EntryPerson { get; set; }
        public string BorrowStatus { get; set; }
        public string Note { get; set; }
        public string? Scrap { get; set; }
        public string Type { get; set; } = "";
    }

}
