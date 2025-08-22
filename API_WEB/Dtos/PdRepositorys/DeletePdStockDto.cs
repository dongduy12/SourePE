using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API_WEB.Dtos.PdRepositorys
{
    public class DeletePdStockDto
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string OutOp { get; set; } = string.Empty;
    }
}