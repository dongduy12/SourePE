using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsOracle
{
    [Table("R_SAMPLING_RECORD_T", Schema = "SFISM4")] // Ánh xạ bảng và schema Oracle
    public class SFISM4_R_SAMPLING_RECORD_T
    {
        public string? TYPE { get; set; } // Loại mẫu kiểm tra
        public string? MO_NUMBER { get; set; }
        public string? MODEL_NAME { get; set; }
        public string? KEY_PART_NO { get; set; }
        public int? TARGET_QTY { get; set; }
        public int? SAMPLE_SIZE { get; set; }
        public int? MAJOR_REJECT_QTY { get; set; }
        public int? PASS_QTY { get; set; }
        public int? FAIL_QTY { get; set; }
        public int? PASS_QTY1 { get; set; }
        public int? FAIL_QTY1 { get; set; }
        public int? PASS_QTY2 { get; set; }
        public int? FAIL_QTY2 { get; set; }
    }
}
