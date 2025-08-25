namespace API_WEB.Models.Bonepile
{
    public class BonepileResult
    {
        public string SN { get; set; }
        public string MODEL_NAME { get; set; }
        public string PRODUCT_LINE { get; set; }
        public string MO_NUMBER { get; set; }
        public string FAIL_STATION { get; set; }
        public string SYMPTOM { get; set; }
        public string ERROR_DESC { get; set; }
        public DateTime TIME { get; set; }
        public string FLAG { get; set; }
        public string PO_NO { get; set; }
        public string PO_ITEM { get; set; }
        public double FAILURE_AGING { get; set; }
        public string WIP_GROUP { get; set; }
        public string VERSION_CODE { get; set; }
        public string WORK_FLAG { get; set; }
        public string ERROR_FLAG { get; set; }
        public string MO_NEW { get; set; }
        public string STATUS { get; set; }
        public DateTime? CHECKIN_REPAIR_TIME { get; set; }
        public DateTime? CHECKOUT_REPAIR_TIME { get; set; }
        public string SCRAP_STATUS { get; set; }
    }

    public class StatusRequest
    {
        public List<string> Statuses { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }

    // Response model for ScrapList Category
    public class ScrapListCategory
    {
        public string SN { get; set; }
        public string Category { get; set; }
    }

    public class StatusRequestBonepile
    {
        public List<string> Statuses { get; set; }
    }

    public class RepairTaskResult
    {
        public string SERIAL_NUMBER { get; set; }
        public string MODEL_NAME { get; set; }
        public string PRODUCT_LINE { get; set; }
        public string MO_NUMBER { get; set; }
        public string TEST_CODE { get; set; }
        public string ERROR_DESC { get; set; }
        public string WIP_GROUP { get; set; }
        public string TEST_GROUP { get; set; }
        public string TEST_TIME { get; set; }
        public string VERSION_CODE { get; set; }
        public string WORK_FLAG { get; set; }
        public string ERROR_FLAG { get; set; }
        public string DATA11 { get; set; }
        public string REPAIR { get; set; }
        public string? STATUS { get; set; }
        public string? AGING_DAY { get; set; }
        public DateTime? CHECKIN_DATE { get; set; }
    }

    // Result model for bonepile after kanban query
    public class BonepileAfterKanbanResult
    {
        public string SERIAL_NUMBER { get; set; }
        public string MO_NUMBER { get; set; }
        public string MODEL_NAME { get; set; }
        public string PRODUCT_LINE { get; set; }
        public string WIP_GROUP_KANBAN { get; set; }
        public string WIP_GROUP_SFC { get; set; }
        public string ERROR_FLAG { get; set; }
        public string WORK_FLAG { get; set; }
        public string TEST_GROUP { get; set; }
        public DateTime? TEST_TIME { get; set; }
        public string TEST_CODE { get; set; }
        public string ERROR_DESC { get; set; }
        public double? AGING { get; set; }
    }

    public class SerialNumberBonepileRequest
    {
        public List<string> SerialNumbers { get; set; }
    }

    public class BonepileAfterKanbanTestInfoResult
    {
        public string SERIAL_NUMBER { get; set; }
        public string TEST_GROUP { get; set; }
        public DateTime? TEST_TIME { get; set; }
        public string TEST_CODE { get; set; }
        public string ERROR_DESC { get; set; }
        public double? AGING { get; set; }
    }
}
