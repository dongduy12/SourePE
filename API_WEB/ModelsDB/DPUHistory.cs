using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace API_WEB.ModelsDB
{
    [Table("DPUHistory")]
    public class DPUHistory
    {
        [Key]
        public int HistoryId { get; set; } // ID tự động tăng
        public string? SerialNumber { get; set; }
        public string? ModelName { get; set; }
        public string? TestCode { get; set; }
        public string? TestGroup { get; set; }
        public string? Data1 { get; set; }
        public string? WIPGroup { get; set; }
        public DateTime? TestTime { get; set; }
        public string? Station { get; set; }
        public string? PreviousStation { get; set; }
        public string? Note { get; set; }
        public string? FTOFF { get; set; }
        public string? HASS { get; set; }
        public string? FTHASS { get; set; }
        public string? DDRTOOL { get; set; }
        public string? NVInstruction { get; set; }
        public string? Repair { get; set; }
        public string? PEInstruction { get; set; }
        public string? OwnerNe { get; set; }
    }
}
