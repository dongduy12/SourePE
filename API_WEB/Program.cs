#nullable disable
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using API_WEB.Services.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using API_WEB.Controllers.Scrap;
using API_WEB.Controllers.SmartFA;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Đảm bảo đọc api_appsettings.json
builder.Configuration
    .AddJsonFile("api_appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"api_appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Thêm CORS
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(MyAllowSpecificOrigins, policy =>
    {
        policy.WithOrigins("http://localhost:5050", "http://10.220.130.119:8000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Thêm các dịch vụ
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.OperationFilter<MultipartFormDataOperationFilter>();
});

// Cấu hình DbContext
builder.Services.AddDbContext<CSDL_NE>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SQLServerConnection"), sqlOptions =>
    {
        sqlOptions.CommandTimeout(300);
    }));

builder.Services.AddDbContext<OracleDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("OracleConnection"))
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine));

// Thêm các hosted services và HttpClient
builder.Services.AddHostedService<DailyTaskService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("RepairStatusClient", client =>
{
    client.BaseAddress = new Uri("https://10.220.130.215:443/SfcSmartRepair/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
});

builder.Services.AddHostedService<AutoExportBackgroundService>();
builder.Services.AddSingleton<IWebHostEnvironment>(builder.Environment);
builder.Services.AddHostedService<ScrapTaskNumberAlertService>();
builder.Services.AddHostedService<TaskWaitMoveScrapStock>();
builder.Services.AddHostedService<Alert>();
builder.Services.AddHostedService<ScrapToProduct>();
builder.Services.AddHostedService<LinkMoCheckingService>();

var app = builder.Build();

app.UseRouting();
app.UseCors(MyAllowSpecificOrigins);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Operation Filter để hỗ trợ multipart/form-data
public class MultipartFormDataOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var parameters = context.MethodInfo.GetParameters();
        if (parameters.Any(p => p.ParameterType == typeof(IFormFile) || p.ParameterType == typeof(IFormFileCollection)))
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema { Type = "string", Format = "binary", Description = "Excel file to import" }
                            },
                            Required = new HashSet<string> { "file" }
                        }
                    }
                }
            };
        }
    }
}