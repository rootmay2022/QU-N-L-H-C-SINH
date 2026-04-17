using ConnectDB.Data;
using ConnectDB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ConnectDB.DTO;
namespace ConnectDB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login)
        {
            if (login == null || string.IsNullOrWhiteSpace(login.Username))
                return BadRequest(new { message = "Nhập thiếu thông tin!" });

            // Tìm user và xóa khoảng trắng 2 đầu
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.Trim() == login.Username.Trim());

            if (user == null)
                return Unauthorized(new { message = "Tài khoản không tồn tại!" });

            // SO SÁNH TRỰC TIẾP (Bỏ qua BCrypt)
            var dbPass = user.Password?.Trim() ?? "";
            var inputPass = login.Password?.Trim() ?? "";

            if (dbPass != inputPass)
            {
                return Unauthorized(new { message = "Mật khẩu không đúng!" });
            }

            // Nếu đúng thì tạo Token
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                token = token,
                username = user.Username,
                role = user.Role,
                message = "Đăng nhập thành công!"
            });
        }
        private string GenerateJwtToken(User user)
        {
            // Lấy Key từ appsettings, nếu null dùng key mặc định (phải dài > 32 ký tự)
            var keyStr = _config["Jwt:Key"] ?? "Chuoi_Key_Bi_Mat_Sieu_Cap_Vip_Phai_Tren_32_Ky_Tu";
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // Class nhận dữ liệu từ Postman/Swagger
    public class LoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}