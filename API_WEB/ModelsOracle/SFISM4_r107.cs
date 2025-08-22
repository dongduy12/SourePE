namespace API_WEB.ModelsOracle
{
    public class SFISM4_r107
    {
        public string SERIAL_NUMBER { set; get; }
        public string MODEL_NAME { set; get; }
        public string? MO_NUMBER { set; get; } = string.Empty;
        public string? WIP_GROUP { set; get; } = string.Empty;
        public string? WORK_FLAG { set; get; } = string.Empty;
        public string? ERROR_FLAG { set; get; } = string.Empty;

        public ICollection<SFISM4_R107_PdStock> SFISM4_R107_PdStocks { get; set; } = new List<SFISM4_R107_PdStock>();
    }

}
