using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.ModelsDB
{
    [Table("Shelf")]
    public partial class Shelf
    {
        [Key]
        [Column("ShelfID")]
        public int ShelfId { get; set; }
        [StringLength(50)]
        public string ShelfCode { get; set; } = null!;
        public int? MaxColumns { get; set; }
        // Navigation Property để liên kết với các Product
        public virtual ICollection<Product> Products { get; set; } = new HashSet<Product>();
    }
}
