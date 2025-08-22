using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PESystem.Models;
using PESystem.Policies;

var builder = WebApplication.CreateBuilder(args);
var configuration = new ConfigurationBuilder()
    .AddJsonFile("mvc_appsettings.json", optional: false, reloadOnChange: true)
    .Build();

builder.Services.AddHttpContextAccessor();
// Đăng ký các dịch vụ
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(configuration["ApiBaseUrl"]!);
});

// Cấu hình chuỗi kết nối đến SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("cnn")));

//Đăng ký handler
builder.Services.AddSingleton<IAuthorizationHandler, AreaAccessHandler>();

//ĐỊnh nghĩa Policy cho mỗi Area
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SmartFAAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("SmartFA")));
    options.AddPolicy("RepositoriesAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("Repositories")));
    options.AddPolicy("PDRepositoriesAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("PDRepositories")));
    options.AddPolicy("DataCloudAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("DataCloud")));
    options.AddPolicy("HassBiAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("HassBi")));
    options.AddPolicy("DDRAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("DDR")));
    options.AddPolicy("CheckListAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("CheckList")));
    options.AddPolicy("ScrapAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("Scrap")));
    options.AddPolicy("MaterialSystemAccess", policy =>
        policy.Requirements.Add(new AreaAccessRequirement("MaterialSystem")));
});


//Chuyển hướng đến action AccessDenied khi username không có quyền truy cập
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
// Đăng ký IPasswordHasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Cấu hình dịch vụ
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // Giới hạn 100 MB
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});



var app = builder.Build();
app.UseCors("AllowAll");

//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    var context = services.GetRequiredService<ApplicationDbContext>();
//    context.Database.Migrate(); // Áp dụng migrations nếu có

//    // Seed Data
//    if (!context.Users.Any())
//    {
//        var hasher = new PasswordHasher<User>();

//        context.Users.Add(new User
//        {
//            Username = "admin",
//            Password = hasher.HashPassword(null, "123"), // Mã hóa mật khẩu
//            Role = "Admin"
//        });

//        context.Users.Add(new User
//        {
//            Username = "user",
//            Password = hasher.HashPassword(null, "userpass"), // Mã hóa mật khẩu
//            Role = "User"
//        });

//        context.SaveChanges();
//    }
//}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseHttpsRedirection();

app.UseStaticFiles();
//app.UseStaticFiles(new StaticFileOptions
//{
//    FileProvider = new PhysicalFileProvider(Path.Combine("D:\\DataCloud", "Temp")),
//    RequestPath = "/Temp"
//});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

