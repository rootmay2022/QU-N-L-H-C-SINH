using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO; // Gọi thư mục DTO của m vào đây
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConnectDB.Controllers
{
    [Authorize] // Bắt buộc phải đăng nhập mới được xài
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UserController(AppDbContext context) { _context = context; }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        private string GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value ?? "";
        }

        // ================= 1. LẤY THÔNG TIN CÁ NHÂN =================
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            if (role == "Student")
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                if (student == null) return NotFound("Không tìm thấy sinh viên");
                return Ok(new { fullName = student.FullName, email = student.Email, phone = student.Phone, address = student.Address });
            }
            else if (role == "Teacher")
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
                if (teacher == null) return NotFound("Không tìm thấy giảng viên");
                return Ok(new { fullName = teacher.FullName, email = teacher.Email, phone = teacher.Phone, address = teacher.Address });
            }

            return BadRequest("Tài khoản này không hỗ trợ xem Profile.");
        }

        // ================= 2. CẬP NHẬT THÔNG TIN =================
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileDto dto)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            if (role == "Student")
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                if (student == null) return NotFound("Không tìm thấy dữ liệu sinh viên");

                student.Email = dto.Email;
                student.Phone = dto.Phone;
                student.Address = dto.Address;
            }
            else if (role == "Teacher")
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
                if (teacher == null) return NotFound("Không tìm thấy dữ liệu giảng viên");

                teacher.Email = dto.Email;
                teacher.Phone = dto.Phone;
                teacher.Address = dto.Address;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thông tin thành công!" });
        }

        // ================= 3. ĐỔI MẬT KHẨU =================
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Tài khoản không tồn tại");

            if (user.Password != dto.OldPassword)
                return BadRequest(new { message = "Mật khẩu cũ không chính xác!" });

            user.Password = dto.NewPassword;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }
    }
}