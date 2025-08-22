using Microsoft.EntityFrameworkCore;
using static API_WEB.Controllers.HassBiController;
using API_WEB.Models.SmartFA;

namespace API_WEB.ModelsOracle
{
    public class OracleDbContext : DbContext
    {
        public OracleDbContext(DbContextOptions<OracleDbContext> options)
            : base(options) { }
        public DbSet<SFISM4_r107> OracleDataR107 { get; set; } = null!;
        public DbSet<SFISM4_r109> OracleDataR109 { get; set; } = null!;
        public DbSet<SFIS1_C_MODEL_DESC_T> OracleDataCModelDesc { get; set; } = null!;
        public DbSet<SFISM4_R107_PdStock> OracleDataR107PdStock { get; set; } = null!;
        public DbSet<SFISM4_R_SAMPLING_RECORD_T> SamplingRecords { get; set; }
        public DbSet<R_REPAIR_TASK_T> OracleDataRepairTask { get; set; } = null!;
        public DbSet<R_REPAIR_TASK_DETAIL_T> OracleDataRepairTaskDetail { get; set; } = null!;
        public DbSet<SFISM4_r108> OracleDataR108 { get; set; } = null!;
        public DbSet<SFISM4_r117> OracleDataR117 { get; set; } = null!;
        public DbSet<SYSTEM_AUTO_BLOCK> OracleSystemBlock { get; set; } = null!;
        public DbSet<SYSTEM_HOLD> OracleSystemHold { get; set; } = null!;
        public DbSet<SFISM4_Z_KANBAN_TRACKING_T> OracleDataZKanbanTracking { get; set; } = null!;
        public DbSet<FailedSerialNumber> FailedSerialNumbers { get; set; } // lấy dữ liệu fail hass-bi
        public DbSet<GetAteSn> GetAteSnRecords { get; set; } // DbSet cho bảng
        public DbSet<ErrorCode> ErrorCodes { get; set; } // DbSet cho bảng
        public DbSet<RepairOwnerBySerialResult> RepairOwnerBySerialResults { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SFISM4_r107>(entity =>
            {
                entity.ToTable("R107", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.MODEL_NAME).HasColumnName("MODEL_NAME");
                entity.Property(e => e.MO_NUMBER).HasColumnName("MO_NUMBER");
                entity.Property(e => e.WIP_GROUP).HasColumnName("WIP_GROUP");
            });
            // Cấu hình cho bảng SFISM4_r109
            modelBuilder.Entity<SFISM4_r109>(entity =>
            {
                entity.ToTable("R109", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.MODEL_NAME).HasColumnName("MODEL_NAME");
                entity.Property(e => e.TEST_CODE).HasColumnName("TEST_CODE");
                entity.Property(e => e.TEST_TIME).HasColumnName("TEST_TIME");
                entity.Property(e => e.DATA1).HasColumnName("DATA1");
                entity.Property(e => e.NEW_PN).HasColumnName("NEW_PN");
                entity.Property(e => e.TEST_GROUP).HasColumnName("TEST_GROUP");
            });

            // Cấu hình cho bảng SFIS1_C_MODEL_DESC_T
            modelBuilder.Entity<SFIS1_C_MODEL_DESC_T>(entity =>
            {
                entity.ToTable("C_MODEL_DESC_T", schema: "SFIS1");
                entity.HasKey(e => e.MODEL_NAME);
                entity.Property(e => e.PRODUCT_LINE).HasColumnName("PRODUCT_LINE");
                entity.Property(e => e.MODEL_NAME).HasColumnName("MODEL_NAME");
                entity.Property(e => e.MODEL_SERIAL).HasColumnName("MODEL_SERIAL");
            });

            // Cấu hình cho bảng SFISM4_R107_PdStock
            modelBuilder.Entity<SFISM4_R107_PdStock>(entity =>
            {
                entity.ToTable("R107", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.CARTON_NO).HasColumnName("CARTON_NO");
                entity.Property(e => e.MODEL_NAME).HasColumnName("MODEL_NAME");

                // Cấu hình mối quan hệ với SFISM4_r107
                entity.HasOne(d => d.SFISM4_r107)
                      .WithMany(p => p.SFISM4_R107_PdStocks)
                      .HasForeignKey(d => d.SERIAL_NUMBER)
                      .HasConstraintName("FK_R107_PdStock_R107");
            });

            modelBuilder.Entity<R_REPAIR_TASK_T>(entity =>
            {
                entity.ToTable("R_REPAIR_TASK_T", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.MO_NUMBER).HasColumnName("MO_NUMBER");
                entity.Property(e => e.MODEL_NAME).HasColumnName("MODEL_NAME");
                entity.Property(e => e.TEST_GROUP).HasColumnName("TEST_GROUP");
                entity.Property(e => e.TEST_CODE).HasColumnName("TEST_CODE");
                entity.Property(e => e.DATA1).HasColumnName("DATA1");
                entity.Property(e => e.DATA11).HasColumnName("DATA11");
                entity.Property(e => e.DATA12).HasColumnName("DATA12");
                entity.Property(e => e.DATE3).HasColumnName("DATE3");
                entity.Property(e => e.DATA13).HasColumnName("DATA13");
                entity.Property(e => e.DATA19).HasColumnName("DATA19");
                entity.Property(e => e.REASON_CODE).HasColumnName("REASON_CODE");
            });


            // Cấu hình mối quan hệ với SFISM4.R_SAMPLING_RECORD_T
            modelBuilder.Entity<SFISM4_R_SAMPLING_RECORD_T>()
                .HasNoKey()
                .ToTable("R_SAMPLING_RECORD_T", schema: "SFISM4");

            // Cấu hình dữ liêu fail Hasss-bi
            modelBuilder.Entity<FailedSerialNumber>().HasNoKey();
            modelBuilder.Entity<RepairOwnerBySerialResult>().HasNoKey();
            base.OnModelCreating(modelBuilder);

            // Cấu hình cho bảng R_GET_ATE_SN_T
            modelBuilder.Entity<GetAteSn>()
            .HasNoKey() // Vì bảng không có khóa chính
            .ToTable("R_GET_ATE_SN_T", schema: "SFISM4"); // Tên bảng và schema trong Oracle


            modelBuilder.Entity<R_REPAIR_TASK_DETAIL_T>(entity =>
            {
                entity.ToTable("R_REPAIR_TASK_DETAIL_T", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.MO_NUMBER).HasColumnName("MO_NUMBER");
                entity.Property(e => e.MODEL_NAME).HasColumnName("MODEL_NAME");
                entity.Property(e => e.TEST_GROUP).HasColumnName("TEST_GROUP");
                entity.Property(e => e.TEST_CODE).HasColumnName("TEST_CODE");
                entity.Property(e => e.DATA1).HasColumnName("DATA1");
                entity.Property(e => e.DATA11).HasColumnName("DATA11");
                entity.Property(e => e.DATA12).HasColumnName("DATA12");
                entity.Property(e => e.DATE3).HasColumnName("DATE3");
                entity.Property(e => e.TESTER).HasColumnName("TESTER");
            });
            modelBuilder.Entity<SFISM4_r117>(entity =>
            {
                entity.ToTable("R117", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.GROUP_NAME).HasColumnName("GROUP_NAME");
                entity.Property(e => e.ERROR_FLAG).HasColumnName("ERROR_FLAG");
                entity.Property(e => e.WIP_GROUP).HasColumnName("WIP_GROUP");
            });            
            modelBuilder.Entity<SFISM4_r108>(entity =>
            {
                entity.ToTable("R108", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.KEY_PART_SN).HasColumnName("KEY_PART_SN");
            });
            modelBuilder.Entity<SFISM4_Z_KANBAN_TRACKING_T>(entity =>
            {
                entity.ToTable("Z_KANBAN_TRACKING_T", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.WIP_GROUP).HasColumnName("WIP_GROUP");
            });
            modelBuilder.Entity<SYSTEM_HOLD>(entity =>
            {
                entity.ToTable("R_SYSTEM_HOLD_T", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.HOLD_REASON).HasColumnName("HOLD_REASON");
            });
            modelBuilder.Entity<SYSTEM_AUTO_BLOCK>(entity =>
            {
                entity.ToTable("R_SYSTEM_AUTO_BLOCK_T", schema: "SFISM4");
                entity.HasKey(e => e.SERIAL_NUMBER);
                entity.Property(e => e.SERIAL_NUMBER).HasColumnName("SERIAL_NUMBER");
                entity.Property(e => e.HOLD_REASON).HasColumnName("HOLD_REASON");
            });

            modelBuilder.Entity<ErrorCode>(entity =>
            {
                entity.ToTable("C_ERROR_CODE_T", schema: "SFIS1");
                entity.HasKey(e => e.ERROR_CODE);
                entity.Property(e => e.ERROR_CODE).HasColumnName("ERROR_CODE");
                entity.Property(e => e.ERROR_DESC).HasColumnName("ERROR_DESC");
            });
        }
    }
}
