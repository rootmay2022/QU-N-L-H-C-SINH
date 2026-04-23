using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net;        // <--- MỚI THÊM ĐỂ GỬI MAIL
using System.Net.Mail;   // <--- MỚI THÊM ĐỂ GỬI MAIL

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

        // ================= 3. GỬI MÃ OTP QUÁ EMAIL =================
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp()
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Tài khoản không tồn tại");

            var role = GetCurrentUserRole();
            string? userEmail = "";

            // Lấy Email từ Profile
            if (role == "Student")
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                userEmail = student?.Email;
            }
            else if (role == "Teacher")
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
                userEmail = teacher?.Email;
            }

            // Bắt lỗi nếu chưa có Email
            if (string.IsNullOrEmpty(userEmail))
                return BadRequest(new { message = "Bạn chưa cập nhật Email trong hồ sơ! Vui lòng cập nhật Thông tin cá nhân trước." });

            // Tạo mã OTP 6 số ngẫu nhiên
            var otp = new Random().Next(100000, 999999).ToString();
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.Now.AddMinutes(5); // Mã sống được 5 phút
            await _context.SaveChangesAsync();

            // Gửi Mail
            try
            {
                // ⚠️ CHÚ Ý: MÀY PHẢI THAY EMAIL VÀ APP PASSWORD CỦA MÀY VÀO 2 DÒNG DƯỚI NÀY
                string myEmail = "nguyenkhanhhung@gmail.com";
                string myAppPassword = "jqfdhderekmhmhwi";

                var mail = new MailMessage(myEmail, userEmail);
                mail.Subject = "MÃ XÁC NHẬN ĐỔI MẬT KHẨU";
                mail.Body = $"Mã OTP của bạn là: {otp}\n\nMã này có hiệu lực trong 5 phút. Vui lòng không chia sẻ cho bất kỳ ai!";

                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(myEmail, myAppPassword),
                    EnableSsl = true
                };
                smtp.Send(mail);

                return Ok(new { message = "Mã OTP đã được gửi vào Email của bạn!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi hệ thống khi gửi mail: " + ex.Message });
            }
        }

        // ================= 4. XÁC NHẬN OTP & ĐỔI MẬT KHẨU =================
        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordDto dto)
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Tài khoản không tồn tại");

            // Kiểm tra mã OTP
            if (user.OtpCode != dto.Otp)
                return BadRequest(new { message = "Mã OTP không chính xác!" });

            // Kiểm tra thời gian hết hạn
            if (user.OtpExpiry < DateTime.Now)
                return BadRequest(new { message = "Mã OTP đã hết hạn! Vui lòng gửi lại mã mới." });

            // Đổi mật khẩu mới
            user.Password = dto.NewPassword;

            // Dọn dẹp OTP sau khi xài xong cho bảo mật
            user.OtpCode = null;
            user.OtpExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }
    }
}