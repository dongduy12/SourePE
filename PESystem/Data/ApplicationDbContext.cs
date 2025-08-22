#nullable disable
using Microsoft.EntityFrameworkCore;
using PESystem.Models;
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cấu hình để lưu AllowedAreas dưới dạng chuỗi
        modelBuilder.Entity<User>()
            .Property(u => u.AllowedAreas)
            .HasConversion(
                v => string.Join(',', v), // Chuyển danh sách thành chuỗi khi lưu vào database
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() // Chuyển chuỗi thành danh sách khi lấy ra
            );

        base.OnModelCreating(modelBuilder); // Gọi phương thức cơ bản nếu cần
    }
}
