using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO; // 👈 Chìa khóa để không bị lỗi là dòng này
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace ConnectDB.Controllers
{
    [Authorize(Roles = "Admin")] // 🔐 Chỉ Admin mới có quyền vào đây
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AdminController(AppDbContext context) { _context = context; }

        // ================= 1. KHOA (FACULTY) =================
        [HttpGet("faculties")]
        public async Task<IActionResult> GetFaculties() => Ok(await _context.Faculties.ToListAsync());

        [HttpPost("faculties")]
        public async Task<IActionResult> AddFaculty([FromBody] Faculty f)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _context.Faculties.Add(f);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm khoa thành công" });
        }

        [HttpPut("faculties/{id}")]
        public async Task<IActionResult> UpdateFaculty(int id, [FromBody] Faculty f)
        {
            var exist = await _context.Faculties.FindAsync(id);
            if (exist == null) return NotFound();
            exist.FacultyName = f.FacultyName;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật khoa thành công" });
        }

        [HttpDelete("faculties/{id}")]
        public async Task<IActionResult> DeleteFaculty(int id)
        {
            var f = await _context.Faculties.FindAsync(id);
            if (f == null) return NotFound();
            _context.Faculties.Remove(f);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa khoa thành công" });
        }

        // ================= 2. MÔN HỌC (SUBJECT) =================
        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects() => Ok(await _context.Subjects.ToListAsync());

        [HttpPost("subjects")]
        public async Task<IActionResult> AddSubject([FromBody] Subject s)
        {
            _context.Subjects.Add(s);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm môn học thành công" });
        }

        [HttpDelete("subjects/{id}")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var s = await _context.Subjects.FindAsync(id);
            if (s == null) return NotFound();
            _context.Subjects.Remove(s);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa môn thành công" });
        }

        // ================= 3. SINH VIÊN (STUDENT) - ĐỒNG BỘ USER =================
        [HttpPost("students")]
        public async Task<IActionResult> AddStudent([FromBody] Student s)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (await _context.Users.AnyAsync(u => u.Username == s.StudentCode))
                return BadRequest(new { message = "Mã sinh viên đã tồn tại trên hệ thống tài khoản!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Username = s.StudentCode,
                    Password = BCrypt.Net.BCrypt.HashPassword("123"),
                    Role = "Student"
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                s.UserId = newUser.Id;
                _context.Students.Add(s);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new { message = "Thêm sinh viên và tạo tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPut("students/{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] Student s)
        {
            var st = await _context.Students.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
            if (st == null) return NotFound();

            if (st.StudentCode != s.StudentCode)
            {
                if (await _context.Users.AnyAsync(u => u.Username == s.StudentCode && u.Id != st.UserId))
                    return BadRequest(new { message = "Mã sinh viên mới đã bị trùng!" });

                if (st.User != null) st.User.Username = s.StudentCode;
            }

            st.FullName = s.FullName;
            st.StudentCode = s.StudentCode;
            st.ClassId = s.ClassId;
            st.Birthday = s.Birthday;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật sinh viên và đồng bộ tài khoản thành công!" });
        }

        [HttpDelete("students/{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var st = await _context.Students.FindAsync(id);
            if (st == null) return NotFound();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(st.UserId);
                _context.Students.Remove(st);
                if (user != null) _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Đã xóa sinh viên và tài khoản liên quan" });
            }
            catch
            {
                await transaction.RollbackAsync();
                return BadRequest("Không thể xóa (có thể sinh viên đã có điểm hoặc lịch học)");
            }
        }

        // ================= 4. GIẢNG VIÊN (TEACHER) =================
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers() => Ok(await _context.Teachers.ToListAsync());

        [HttpPost("teachers")]
        public async Task<IActionResult> AddTeacher([FromBody] CreateTeacherDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Username = dto.Username,
                    Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Role = "Teacher",
                    FullName = dto.FullName
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var newTeacher = new Teacher
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    UserId = newUser.Id,
                    TeacherCode = "GV" + newUser.Id
                };
                _context.Teachers.Add(newTeacher);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new { message = "Thành công!", userId = newUser.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest("Lỗi hệ thống: " + ex.Message);
            }
        }

        [HttpDelete("teachers/{id}")]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            var t = await _context.Teachers.FindAsync(id);
            if (t == null) return NotFound();

            var user = await _context.Users.FindAsync(t.UserId);

            _context.Teachers.Remove(t);
            if (user != null) _context.Users.Remove(user);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa giảng viên và tài khoản thành công" });
        }

        // ================= 5. LỊCH HỌC (SCHEDULE) =================
        [HttpGet("schedules")]
        public async Task<IActionResult> GetSchedules()
        {
            return Ok(await _context.Schedules
                .Include(s => s.Subject).Include(s => s.Teacher).Include(s => s.Class)
                .ToListAsync());
        }

        [HttpPost("schedules")]
        public async Task<IActionResult> AddSchedule([FromBody] Schedule sc)
        {
            _context.Schedules.Add(sc);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã thêm lịch học" });
        }

        // ================= 6. LỚP HỌC (CLASS) =================
        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses() => Ok(await _context.Classes.Include(c => c.Faculty).ToListAsync());

        [HttpPost("classes")]
        public async Task<IActionResult> AddClass([FromBody] Class c)
        {
            _context.Classes.Add(c);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm lớp thành công" });
        }

        // ================= 7. ĐIỂM SỐ (SCORE) =================
        [HttpGet("scores")]
        public async Task<IActionResult> GetAllScores()
        {
            return Ok(await _context.Scores.Include(s => s.Student).Include(s => s.Subject).ToListAsync());
        }

        [HttpPost("scores")]
        public async Task<IActionResult> AddScore([FromBody] Score s)
        {
            _context.Scores.Add(s);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Nhập điểm thành công!" });
        }

        // ================= 8. HỆ THỐNG TÀI KHOẢN (USER SYSTEM) =================
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers() => Ok(await _context.Users.ToListAsync());

        [HttpPut("users/reset-password/{id}")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Password = BCrypt.Net.BCrypt.HashPassword("123");
            await _context.SaveChangesAsync();
            return Ok(new { message = "Mật khẩu đã reset về 123" });
        }

        [HttpPost("create-user-manual")]
        public async Task<IActionResult> CreateUserManual([FromBody] User u)
        {
            if (await _context.Users.AnyAsync(x => x.Username == u.Username))
                return BadRequest(new { message = "Username đã tồn tại!" });

            u.Password = BCrypt.Net.BCrypt.HashPassword(u.Password);
            _context.Users.Add(u);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Tạo tài khoản thành công!" });
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] User u)
        {
            var userDb = await _context.Users.FindAsync(id);
            if (userDb == null) return NotFound();

            userDb.Username = u.Username;
            userDb.Role = u.Role;
            if (!string.IsNullOrEmpty(u.Password))
                userDb.Password = BCrypt.Net.BCrypt.HashPassword(u.Password);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thành công" });
        }

        // ================= 9. NGHIỆP VỤ ĐẶC BIỆT =================
        [HttpPut("leave-requests/approve/{id}")]
        public async Task<IActionResult> ApproveLeave(int id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null) return NotFound();

            request.Status = "Approved";

            var busySchedules = await _context.Schedules
                .Where(s => s.TeacherId == request.TeacherId && s.Date.Date == request.OffDate.Date)
                .ToListAsync();

            foreach (var item in busySchedules)
                item.Note = "GIẢNG VIÊN NGHỈ - LỚP TỰ HỌC";

            await _context.SaveChangesAsync();
            return Ok(new { message = "Duyệt nghỉ thành công!" });
        }
    }
}