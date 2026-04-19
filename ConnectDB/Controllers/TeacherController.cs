using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO; // Thêm cái này để nhận ScoreDto m đã tạo
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConnectDB.Controllers
{
    [Authorize(Roles = "Admin,Teacher")]
    [ApiController]
    [Route("api/[controller]")]
    public class TeacherController : ControllerBase
    {
        private readonly AppDbContext _context;
        public TeacherController(AppDbContext context) { _context = context; }

        // Hàm hỗ trợ lấy TeacherId từ Token
        private int GetCurrentTeacherId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return 0;

            var userId = int.Parse(userIdClaim.Value);
            return _context.Teachers.FirstOrDefault(t => t.UserId == userId)?.Id ?? 0;
        }

        // ================= 1. XEM LỊCH DẠY & ĐƠN NGHỈ =================
        [HttpGet("my-schedules")]
        public async Task<IActionResult> GetSchedules()
        {
            var teacherId = GetCurrentTeacherId();
            var data = await _context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.Class)
                .Where(s => s.TeacherId == teacherId)
                .ToListAsync();
            return Ok(data);
        }

        [HttpPost("request-leave")]
        public async Task<IActionResult> RequestLeave([FromBody] LeaveRequest lr)
        {
            lr.TeacherId = GetCurrentTeacherId();
            lr.Status = "Pending";
            _context.LeaveRequests.Add(lr);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã gửi đơn xin nghỉ chờ Admin duyệt" });
        }

        // ================= 2. QUẢN LÝ SINH VIÊN & ĐIỂM DANH =================
        [HttpGet("class-students/{classId}")]
        public async Task<IActionResult> GetStudentsByClass(int classId)
        {
            var students = await _context.Students
                .Where(s => s.ClassId == classId)
                .Select(s => new { s.Id, s.StudentCode, s.FullName })
                .ToListAsync();
            return Ok(students);
        }

        [HttpPost("take-attendance")]
        public async Task<IActionResult> TakeAttendance([FromBody] List<AttendanceDto> list)
        {
            foreach (var item in list)
            {
                var att = new Attendance
                {
                    ScheduleId = item.ScheduleId,
                    StudentId = item.StudentId,
                    IsPresent = item.IsPresent,
                    Date = DateTime.Now
                };
                _context.Attendances.Add(att);
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Điểm danh thành công!" });
        }

        // ================= 3. NHẬP & SỬA ĐIỂM (Đã fix lỗi Value) =================
        [HttpPost("submit-scores")]
        public async Task<IActionResult> SubmitScore([FromBody] ScoreDto dto)
        {
            var teacherId = GetCurrentTeacherId();

            // 1. Kiểm tra quyền giảng dạy
            var canGrade = await _context.Schedules.AnyAsync(s => s.TeacherId == teacherId && s.SubjectId == dto.SubjectId);
            if (!canGrade) return Forbid();

            // 2. Tìm hoặc tạo mới bản ghi điểm
            var exist = await _context.Scores
                .FirstOrDefaultAsync(s => s.StudentId == dto.StudentId && s.SubjectId == dto.SubjectId);

            if (exist != null)
            {
                exist.KT1 = dto.KT1;
                exist.KT2 = dto.KT2;
                exist.DiemThi = dto.DiemThi;

                exist.DiemTrungBinh = (float)Math.Round((exist.KT1 + exist.KT2 + exist.DiemThi * 2) / 4, 1);
                exist.KetQua = exist.DiemTrungBinh >= 5 ? "Qua môn" : "Học lại";
            }
            else
            {
                var newScore = new Score
                {
                    StudentId = dto.StudentId,
                    SubjectId = dto.SubjectId,
                    KT1 = dto.KT1,
                    KT2 = dto.KT2,
                    DiemThi = dto.DiemThi,
                    DiemTrungBinh = (float)Math.Round((dto.KT1 + dto.KT2 + dto.DiemThi * 2) / 4, 1),
                    KetQua = ((dto.KT1 + dto.KT2 + dto.DiemThi * 2) / 4) >= 5 ? "Qua môn" : "Học lại"
                };
                _context.Scores.Add(newScore);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Lưu điểm thành công!" });
        }
    }

    // --- DTOs ĐƯỢC ĐƯA RA NGOÀI CLASS CONTROLLER ĐỂ TRÁNH LỖI ---
    public class AttendanceDto
    {
        public int ScheduleId { get; set; }
        public int StudentId { get; set; }
        public bool IsPresent { get; set; }
    }
}