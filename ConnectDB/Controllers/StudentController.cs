using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace ConnectDB.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StudentController(AppDbContext context) { _context = context; }

        // Hàm lấy UserId từ Token JWT
        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        // 1. Lấy hồ sơ cá nhân
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            var student = await _context.Students
                .Include(s => s.Class)
                .Where(s => s.UserId == userId)
                .Select(s => new {
                    s.StudentCode,
                    s.FullName,
                    s.Birthday,
                    s.Gender,
                    s.Phone,
                    s.Email,
                    s.Address,
                    s.Status,
                    ClassName = s.Class != null ? s.Class.ClassName : "N/A"
                })
                .FirstOrDefaultAsync();

            if (student == null) return NotFound(new { message = "Không tìm thấy hồ sơ sinh viên" });
            return Ok(student);
        }

        // 2. Xem thời khóa biểu cá nhân
        [HttpGet("my-schedules")]
        public async Task<IActionResult> GetMySchedules()
        {
            var userId = GetCurrentUserId();
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null) return NotFound(new { message = "Không tìm thấy thông tin sinh viên." });

            var schedules = await _context.Schedules
                .Where(s => s.ClassId == student.ClassId)
                .Include(s => s.Subject)
                .Include(s => s.Teacher)
                .OrderBy(s => s.LearnDate)
                .ThenBy(s => s.Slot)
                .Select(s => new {
                    s.LearnDate,
                    s.Slot,
                    s.Room,
                    SubjectName = s.Subject != null ? s.Subject.SubjectName : "N/A",
                    TeacherName = s.Teacher != null ? s.Teacher.FullName : "N/A"
                })
                .ToListAsync();

            return Ok(schedules);
        }

        // 3. Đổi mật khẩu (So sánh chuỗi trực tiếp - Không mã hóa)
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Người dùng không tồn tại" });

            // So sánh trực tiếp
            if (user.Password != null && user.Password.Trim() != dto.OldPassword.Trim())
                return BadRequest(new { message = "Mật khẩu cũ không chính xác" });

            user.Password = dto.NewPassword.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đổi mật khẩu thành công" });
        }

        // 4. Cập nhật hồ sơ
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ConnectDB.DTO.StudentUpdateDto dto)
        {
            var userId = GetCurrentUserId();
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null) return NotFound(new { message = "Không tìm thấy sinh viên" });

            student.FullName = dto.FullName.Trim();
            student.Phone = dto.Phone?.Trim();
            student.Address = dto.Address?.Trim();
            student.Email = dto.Email?.Trim();
            student.Birthday = dto.Birthday;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật hồ sơ thành công" });
        }

        // 5. Bảng điểm và Xếp loại (Đã fix lỗi s.Value -> s.DiemTrungBinh)
        [HttpGet("academic-summary")]
        public async Task<IActionResult> GetSummary()
        {
            var userId = GetCurrentUserId();
            var student = await _context.Students
                .Include(s => s.Class)
                .Include(s => s.Scores!)
                    .ThenInclude(sc => sc.Subject)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null) return NotFound(new { message = "Không tìm thấy hồ sơ" });

            if (student.Scores == null || !student.Scores.Any())
                return Ok(new { message = "Chưa có dữ liệu điểm học tập" });

            var subjectDetails = student.Scores.Select(s => new SubjectGradeDto
            {
                SubjectName = s.Subject?.SubjectName ?? "N/A",
                Score = s.DiemTrungBinh,
                Grade = s.DiemTrungBinh >= 8.5 ? "A" : s.DiemTrungBinh >= 7.0 ? "B" : s.DiemTrungBinh >= 5.5 ? "C" : s.DiemTrungBinh >= 4.0 ? "D" : "F",
                Status = s.KetQua ?? "Chưa có kết quả"
            }).ToList();

            double avg = student.Scores.Average(x => x.DiemTrungBinh);
            string ranking = avg >= 8.0 ? "Giỏi" : avg >= 6.5 ? "Khá" : avg >= 5.0 ? "Trung bình" : "Yếu";

            return Ok(new AcademicSummaryDto
            {
                StudentName = student.FullName,
                ClassName = student.Class?.ClassName ?? "N/A",
                AverageGPA = Math.Round(avg, 2),
                Ranking = ranking,
                SubjectDetails = subjectDetails
            });
        }
    }

    // --- DTOs ---
    public class ChangePasswordDto
    {
        [Required]
        public string OldPassword { get; set; } = "";
        [Required, MinLength(6)]
        public string NewPassword { get; set; } = "";
    }

    public class AcademicSummaryDto
    {
        public string StudentName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public double AverageGPA { get; set; }
        public string Ranking { get; set; } = "";
        public List<SubjectGradeDto> SubjectDetails { get; set; } = new();
    }

    public class SubjectGradeDto
    {
        public string SubjectName { get; set; } = "";
        public double Score { get; set; }
        public string Grade { get; set; } = "";
        public string Status { get; set; } = "";
    }
}