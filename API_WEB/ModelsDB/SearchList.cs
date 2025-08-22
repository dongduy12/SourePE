using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("SearchLists")]
    public class SearchList
    {
        public int Id { get; set; }
        public string ListName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public List<SearchListItem> SearchListItems { get; set; }
    }
}