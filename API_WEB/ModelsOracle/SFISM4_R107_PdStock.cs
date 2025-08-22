using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API_WEB.ModelsOracle
{
    public class SFISM4_R107_PdStock
    {
        public string SERIAL_NUMBER { set; get; } = string.Empty;
        public string CARTON_NO { set; get; } = string.Empty;
        public string MODEL_NAME { set; get; } = string.Empty;
        // Thêm thuộc tính điều hướng
        public SFISM4_r107 SFISM4_r107 { get; set; } = null!;
    }
}