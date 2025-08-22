using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_WEB.ModelsDB
{
    [Table("InternalTaskCounter")] // Đặt tên bảng tương ứng trong SQL Server
    public class InternalTaskCounter
    {
        public int Id { get; set; }
        public string CounterName { get; set; }
        public int LastValue { get; set; }
    }
}
