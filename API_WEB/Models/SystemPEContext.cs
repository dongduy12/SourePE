using System;
using System.Collections.Generic;
using API_WEB.ModelsDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace API_WEB.Models
{
    public partial class SystemPEContext : DbContext
    {
        public SystemPEContext()
        {
        }
        public SystemPEContext(DbContextOptions<SystemPEContext> options)
            : base(options)
        {
        }
        public virtual DbSet<Export> Exports { get; set; } = null!;
        public virtual DbSet<PdStock> PdStocks { get; set; } = null!;
        public virtual DbSet<PdStockHistory> PdStockHistories { get; set; } = null!;
        public virtual DbSet<Product> Products { get; set; } = null!;
        public virtual DbSet<Shelf> Shelves { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlServer("Data Source=10.220.130.119,5999;Initial Catalog=SystemPE;Persist Security Info=True;User ID=PE-05;Password=Foxconn168!!;TrustServerCertificate=True;");
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Export>(entity =>
            {
                entity.ToTable("Export");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.EntryDate).HasColumnType("datetime");

                entity.Property(e => e.EntryPerson).HasMaxLength(100);

                entity.Property(e => e.ExportDate).HasColumnType("datetime");

                entity.Property(e => e.ExportPerson).HasMaxLength(100);

                entity.Property(e => e.ModelName).HasMaxLength(50);

                entity.Property(e => e.ProductLine).HasMaxLength(100);

                entity.Property(e => e.SerialNumber)
                    .HasMaxLength(100)
                    .HasColumnName("SERIAL_NUMBER");
            });
            modelBuilder.Entity<PdStock>(entity =>
            {
                entity.Property(e => e.CartonNo)
                    .HasMaxLength(50)
                    .HasColumnName("CARTON_NO");
                entity.Property(e => e.EntryDate)
                    .HasColumnType("datetime")
                    .HasColumnName("ENTRY_DATE");
                entity.Property(e => e.EntryOp)
                    .HasMaxLength(50)
                    .HasColumnName("ENTRY_OP");
                entity.Property(e => e.LocationStock)
                    .HasMaxLength(50)
                    .HasColumnName("LOCATION_STOCK");
                entity.Property(e => e.ModelName)
                    .HasMaxLength(50)
                    .HasColumnName("MODEL_NAME");
                entity.Property(e => e.SerialNumber)
                    .HasMaxLength(50)
                    .HasColumnName("SERIAL_NUMBER");
            });

            modelBuilder.Entity<PdStockHistory>(entity =>
            {
                // entity.HasKey(e => e.SnId);

                entity.ToTable("PdStockHistory");

                entity.Property(e => e.Id)
                     .ValueGeneratedNever()
                     .HasColumnName("SN_ID");

                entity.Property(e => e.CartonNo)
                    .HasMaxLength(50)
                    .HasColumnName("CARTON_NO");

                entity.Property(e => e.EntryDate)
                    .HasColumnType("datetime")
                    .HasColumnName("ENTRY_DATE");

                entity.Property(e => e.EntryOp)
                    .HasMaxLength(50)
                    .HasColumnName("ENTRY_OP");

                entity.Property(e => e.LocationStock)
                    .HasMaxLength(50)
                    .HasColumnName("LOCATION_STOCK");

                entity.Property(e => e.ModelName)
                    .HasMaxLength(50)
                    .HasColumnName("MODEL_NAME");

                entity.Property(e => e.OutDate)
                    .HasColumnType("datetime")
                    .HasColumnName("OUT_DATE");

                entity.Property(e => e.OutOp)
                    .HasMaxLength(50)
                    .HasColumnName("OUT_OP");

                entity.Property(e => e.SerialNumber)
                    .HasMaxLength(50)
                    .HasColumnName("SERIAL_NUMBER");
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Product");

                entity.Property(e => e.ProductId).HasColumnName("ProductID");

                entity.Property(e => e.BorrowDate).HasColumnType("datetime");

                entity.Property(e => e.BorrowPerson).HasMaxLength(100);

                entity.Property(e => e.BorrowStatus).HasMaxLength(50);

                entity.Property(e => e.EntryDate).HasColumnType("datetime");

                entity.Property(e => e.EntryPerson).HasMaxLength(100);

                entity.Property(e => e.ModelName).HasMaxLength(50);

                entity.Property(e => e.ProductLine).HasMaxLength(100);

                entity.Property(e => e.SerialNumber).HasMaxLength(100);

                entity.Property(e => e.ShelfId).HasColumnName("ShelfID");
            });

            modelBuilder.Entity<Shelf>(entity =>
            {
                entity.ToTable("Shelf");

                entity.Property(e => e.ShelfId)
                    .ValueGeneratedNever()
                    .HasColumnName("ShelfID");

                entity.Property(e => e.ShelfCode).HasMaxLength(50);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.AllowedAreas).HasDefaultValueSql("(N'')");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
