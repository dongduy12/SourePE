#nullable disable
using System;
using System.Collections.Generic;
using API_WEB.ModelsDB;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using YourNamespace.Models;

namespace API_WEB.ModelsDB
{
    public partial class CSDL_NE : DbContext
    {
        public CSDL_NE() { }
        public CSDL_NE(DbContextOptions<CSDL_NE> options)
            : base(options) { }
        public virtual DbSet<Export> Exports { get; set; } = null!;
        public virtual DbSet<PdStock> PdStocks { get; set; } = null!;
        public virtual DbSet<PdStockHistory> PdStockHistories { get; set; } = null!;
        public virtual DbSet<Product> Products { get; set; } = null!;
        public virtual DbSet<BorrowHistory> BorrowHistories { get; set; } = null!;
        public virtual DbSet<Shelf> Shelves { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;
        public virtual DbSet<HassBi> HassBis { get; set; } // DbSet ánh xạ tới bảng HassBi

        public virtual DbSet<GuideRecords> GuideRecords { get; set; }
        public virtual DbSet<RepairHistory> RepairHistory { get; set; }
        public virtual DbSet<RepairAction> RepairAction { get; set; }
        public virtual DbSet<DPUHistory> DPUHistory { get; set; } = null!;
        public virtual DbSet<DPUCurrent> DPUCurrent { get; set; } = null!;
        public virtual DbSet<CheckInGuides> CheckInGuides { get; set; } = null!;
        public virtual DbSet<SearchList> SearchLists { get; set; } = null!;
        public virtual DbSet<SearchListItem> SearchListItems { get; set; } = null!;
        public DbSet<CheckList> CheckLists { get; set; }
        public virtual DbSet<ScrapList> ScrapLists { get; set; }
        public DbSet<InternalTaskCounter> InternalTaskCounters { get; set; }

        public virtual DbSet<HistoryMaterial> HistoryMaterials { get; set; }
        public virtual DbSet<SumMaterial> SumMaterials { get; set; }
        public virtual DbSet<RetestResult> RetestResult { get; set; }
        public virtual DbSet<KhoScrap> KhoScraps { get; set; }
        public virtual DbSet<LogKhoScrap> Logs { get; set; }
        public virtual DbSet<ProductOld> ProductOlds { get; set; }
        public virtual DbSet<KhoOk> KhoOks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Export>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CheckingB36R).HasDefaultValue(0);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Product"); // Tên bảng trong cơ sở dữ liệu
                entity.HasKey(e => e.ProductId); // Xác định khóa chính

                entity.Property(e => e.SerialNumber)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.BorrowStatus)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("Available");

                entity.Property(e => e.ProductLine)
                    .HasMaxLength(100);

                entity.HasOne(e => e.Shelf) // Thiết lập quan hệ với bảng Shelf
                    .WithMany(s => s.Products) // Một Shelf có nhiều Product
                    .HasForeignKey(e => e.ShelfId) // Khóa ngoại là ShelfId
                    .OnDelete(DeleteBehavior.Restrict); // Hành vi khi xóa Shelf
            });

            modelBuilder.Entity<BorrowHistory>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.AllowedAreas).HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<Shelf>(entity =>
            {
                entity.ToTable("Shelf");
                entity.HasKey(e => e.ShelfId);

                entity.Property(e => e.ShelfCode)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.MaxColumns)
                    .IsRequired(false);
            });

            modelBuilder.Entity<RepairAction>()
                .HasOne(a => a.RepairHistory)
                .WithMany(h => h.RepairActions)
                .HasForeignKey(a => a.RepairHistoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HassBi>().ToTable("HassBi");

            // Ánh xạ đúng tên bảng HistoryMaterial và SumMaterial
            modelBuilder.Entity<HistoryMaterial>()
                .ToTable("HistoryMaterial");

            modelBuilder.Entity<SumMaterial>()
                .ToTable("SumMaterial");

            OnModelCreatingPartial(modelBuilder);

            modelBuilder.Entity<CheckList>()
            .Property(c => c.FA)
            .HasColumnType("nvarchar(MAX)");

            modelBuilder.Entity<CheckList>()
                .Property(c => c.Action)
                .HasColumnType("nvarchar(MAX)");


            // Ánh xạ SearchList tới bảng SearchLists
            modelBuilder.Entity<SearchList>()
                .ToTable("SearchLists")
                .HasKey(sl => sl.Id);

            // Ánh xạ SearchListItem tới bảng SearchListItems
            modelBuilder.Entity<SearchListItem>()
                .ToTable("SearchListItems")
                .HasKey(sli => sli.Id);

            // Ánh xạ SearchListItem tới bảng RetestResult
            modelBuilder.Entity<RetestResult>()
                .ToTable("RetestResults")
                .HasKey(sli => sli.Id);

            modelBuilder.Entity<ProductOld>()
                .ToTable("ProductOld")
                .HasKey(sli => sli.SERIAL_NUMBER);
    }
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

