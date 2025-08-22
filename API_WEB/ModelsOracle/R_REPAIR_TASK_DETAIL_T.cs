using Microsoft.AspNetCore.Mvc;

namespace API_WEB.ModelsOracle
{
    public class R_REPAIR_TASK_DETAIL_T
    {
        public string SERIAL_NUMBER { set; get; } = string.Empty;
        public string MO_NUMBER { set; get; } = string.Empty;
        public string MODEL_NAME { set; get; } = string.Empty;
        public string TEST_GROUP { set; get; } = string.Empty;
        public string TEST_CODE { set; get; } = string.Empty;
        public string? DATA1 { set; get; } = string.Empty;
        public string DATA11 { set; get; } = string.Empty;
        public string DATA12 { set; get; } = string.Empty;
        public DateTime DATE3 { set; get; }
        public string? TESTER { set; get; } = string.Empty;
        public string? DATA13 { set; get; } = string.Empty;
        public string? DATA17 { set; get; } = string.Empty;
        public string? DATA18 { set; get; } = string.Empty;
        public string? DATA19 { set; get; } = string.Empty;

        public ICollection<R_REPAIR_TASK_DETAIL_T> R_REPAIR_TASK_DETAILs { get; set; } = new List<R_REPAIR_TASK_DETAIL_T>();
    }
}
