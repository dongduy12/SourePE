using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.ModelsDB
{
    [Table("Product")]
    public partial class Product
    {
        [Key]
        [Column("ProductID")]
        public int ProductId { get; set; }
        [StringLength(100)]
        public string SerialNumber { get; set; } = null!;
        [Column("ShelfID")]
        public int? ShelfId { get; set; }
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        [StringLength(50)]
        public string BorrowStatus { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? EntryDate { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? BorrowDate { get; set; }
        [StringLength(100)]
        public string? BorrowPerson { get; set; }
        [StringLength(100)]
        public string? EntryPerson { get; set; }
        [StringLength(100)]
        public string? ProductLine { get; set; }
        public int? PositionInTray { get; set; }
        public string? ModelName { get; set; }
        public string? KANBAN_WIP { get; set; }

        [StringLength(200)]
        public string? Note { get; set; }
        public string? Action { get; set; }

        [StringLength(300)]
        public string? Scrap { get; set; }
        // Navigation Property để liên kết với Shelf
        public virtual Shelf? Shelf { get; set; }
    }
}
