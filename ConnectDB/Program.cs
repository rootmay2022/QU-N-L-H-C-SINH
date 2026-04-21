using ConnectDB.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// --- FIX LỖI SẬP SERVER RENDER (INOTIFY LIMIT) ---
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
// --------------------------------------------------

// --- 1. ĐĂNG KÝ MYSQL ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// --- 2. Cấu hình CORS (Mở cửa hoàn toàn cho Frontend) ---
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- 3. Cấu hình Controllers & JSON ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Chống vòng lặp vô tận khi trả về JSON
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // Đảm bảo Frontend nhận key kiểu camelCase (fullName thay vì FullName)
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// --- 4. Cấu hình Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hệ thống Quản lý Sinh viên - NGUYỄN KHÁNH HÙNG", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập Token theo cú pháp: Bearer [token]",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// --- 5. CẤU HÌNH JWT AUTHENTICATION ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? "Chuoi_Key_Mac_Dinh_Sieu_Dai_Phai_Tren_32_Ky_Tu";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ConnectDB_Server";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ConnectDB_Client";

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// --- 6. CẤU HÌNH CỔNG CHO RENDER ---
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// --- 7. CẤU HÌNH MIDDLEWARE (THỨ TỰ RẤT QUAN TRỌNG) ---

// Swagger luôn chạy để m dễ debug
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hệ thống Quản lý Sinh viên V1");
    c.RoutePrefix = string.Empty; // Để vào thẳng trang chính là thấy Swagger
});

// QUAN TRỌNG: UseCors phải nằm trước Authentication và Authorization
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();  