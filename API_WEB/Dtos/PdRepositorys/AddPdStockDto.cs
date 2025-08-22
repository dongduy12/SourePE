using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace API_WEB.Dtos.PdRepositorys
{
    public class AddPdStockDto
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string CartonNo { get; set; } = string.Empty;
        public string LocationStock { get; set; } = string.Empty; 
        public string EntryOp { get; set; } = string.Empty;
    }
}
