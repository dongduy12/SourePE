using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    public class ProductOld
    {
        [Key]
        [Column("SERIAL_NUMBER", TypeName = "VARCHAR(50)")]
        public string SERIAL_NUMBER { get; set; } = null!;

        [Column("PRODUCT_LINE", TypeName = "VARCHAR(50)")]
        public string? PRODUCT_LINE { get; set; }

        [Column("MODEL_NAME", TypeName = "VARCHAR(50)")]
        public string? MODEL_NAME { get; set; }

        [Column("MO_NUMBER", TypeName = "VARCHAR(50)")]
        public string? MO_NUMBER { get; set; }

        [Column("WIP_GROUP", TypeName = "VARCHAR(50)")]
        public string? WIP_GROUP { get; set; }

        [Column("WORK_FLAG", TypeName = "VARCHAR(50)")]
        public string? WORK_FLAG { get; set; }

        [Column("TEST_GROUP", TypeName = "VARCHAR(50)")]
        public string? TEST_GROUP { get; set; }

        [Column("REASON_CODE", TypeName = "VARCHAR(50)")]
        public string? REASON_CODE { get; set; }

        [Column("TEST_CODE", TypeName = "VARCHAR(1000)")]
        public string? TEST_CODE { get; set; }

        [Column("ERROR_DESC", TypeName = "VARCHAR(1000)")]
        public string? ERROR_DESC { get; set; }        

        [Column("KANBAN_WIP", TypeName = "VARCHAR(50)")]
        public string? KANBAN_WIP { get; set; }
    }
}
