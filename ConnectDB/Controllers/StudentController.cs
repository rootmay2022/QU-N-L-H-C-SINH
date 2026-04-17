using ConnectDB.Data;
using ConnectDB.Models;
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

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        // 1. Lấy hồ sơ cá nhân (Đã dọn rác bằng Select)
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

            if (!schedules.Any()) return Ok(new { message = "Bạn chưa có lịch học nào." });

            return Ok(schedules);
        }

        // 3. Đổi mật khẩu (Đã thêm Validation & Trim)
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Người dùng không tồn tại" });

            // Lưu ý: Nếu DB lưu pass đã hash, chỗ này phải dùng hàm Verify của thư viện Hash
            if (user.Password?.Trim() != dto.OldPassword.Trim())
                return BadRequest(new { message = "Mật khẩu cũ không chính xác" });

            user.Password = dto.NewPassword.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đổi mật khẩu thành công" });
        }

        // 4. Cập nhật hồ sơ (Đã thêm Validation)
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] StudentUpdateDto dto)
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

        // 5. Bảng điểm và Xếp loại (Đã fix lỗi Logic Average)
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
                Score = s.Value,
                Grade = s.Value >= 8.5 ? "A" : s.Value >= 7.0 ? "B" : s.Value >= 5.5 ? "C" : s.Value >= 4.0 ? "D" : "F",
                Status = s.Value >= 4.0 ? "Đạt" : "Học lại"
            }).ToList();

            // Fix lỗi Average khi danh sách rỗng (dù đã check Any ở trên nhưng viết vầy cho chắc)
            double avg = student.Scores.Any() ? student.Scores.Average(x => x.Value) : 0;
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

    // --- DTOs (Data Transfer Objects) với Validation ---

    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu cũ")]
        public string OldPassword { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải từ 6 ký tự trở lên")]
        public string NewPassword { get; set; } = "";
    }

    public class StudentUpdateDto
    {
        [Required(ErrorMessage = "Họ tên không được để trống")]
        [StringLength(100, ErrorMessage = "Tên quá dài")]
        public string FullName { get; set; } = "";

        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
        public string? Phone { get; set; }

        public string? Address { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        public DateTime Birthday { get; set; }
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