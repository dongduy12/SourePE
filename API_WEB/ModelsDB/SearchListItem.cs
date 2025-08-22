using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("SearchListItem")]
    public class SearchListItem
    {
        public int Id { get; set; }
        public int SearchListId { get; set; }
        public string? SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? ShelfCode { get; set; }
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        public int? PositionInTray { get; set; }
        public bool IsFound { get; set; }
    }
}
