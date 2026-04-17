using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace ConnectDB.Controllers
{
    [Authorize(Roles = "Admin")]
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
        public async Task<IActionResult> AddFaculty([FromBody] FacultyDto dto)
        {
            var f = new Faculty { FacultyName = dto.FacultyName };
            _context.Faculties.Add(f);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm khoa thành công" });
        }

        [HttpPut("faculties/{id}")]
        public async Task<IActionResult> UpdateFaculty(int id, [FromBody] FacultyDto dto)
        {
            var exist = await _context.Faculties.FindAsync(id);
            if (exist == null) return NotFound();
            exist.FacultyName = dto.FacultyName;
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
        public async Task<IActionResult> AddSubject([FromBody] SubjectDto dto)
        {
            var s = new Subject { SubjectName = dto.SubjectName, Credits = dto.Credits };
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

        // ================= 3. SINH VIÊN (STUDENT) =================
        [HttpPost("students")]
        public async Task<IActionResult> AddStudent([FromBody] StudentCreateDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Username == dto.StudentCode))
                return BadRequest(new { message = "Mã sinh viên đã tồn tại!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Username = dto.StudentCode,
                    Password = BCrypt.Net.BCrypt.HashPassword("123"),
                    Role = "Student"
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var s = new Student
                {
                    StudentCode = dto.StudentCode,
                    FullName = dto.FullName,
                    Birthday = dto.Birthday,
                    Gender = dto.Gender,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    Address = dto.Address,
                    ClassId = dto.ClassId,
                    UserId = newUser.Id
                };
                _context.Students.Add(s);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new { message = "Thêm sinh viên thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("students/{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentCreateDto dto)
        {
            var st = await _context.Students.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
            if (st == null) return NotFound();

            st.FullName = dto.FullName;
            st.ClassId = dto.ClassId;
            st.Birthday = dto.Birthday;
            st.Gender = dto.Gender;
            st.Phone = dto.Phone;
            st.Email = dto.Email;
            st.Address = dto.Address;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thành công!" });
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
                return Ok(new { message = "Đã xóa sinh viên" });
            }
            catch
            {
                await transaction.RollbackAsync();
                return BadRequest("Lỗi khi xóa!");
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

                var t = new Teacher
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    UserId = newUser.Id,
                    TeacherCode = "GV" + newUser.Id
                };
                _context.Teachers.Add(t);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Thêm giảng viên thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(ex.Message);
            }
        }

        // ================= 5. LỊCH HỌC (SCHEDULE) =================
        [HttpPost("schedules")]
        public async Task<IActionResult> AddSchedule([FromBody] ScheduleCreateDto dto)
        {
            var sc = new Schedule
            {
                Date = dto.LearnDate,
                LearnDate = dto.LearnDate,
                Slot = dto.Slot,
                Room = dto.Room,
                SubjectId = dto.SubjectId,
                TeacherId = dto.TeacherId,
                ClassId = dto.ClassId,
                Note = dto.Note
            };
            _context.Schedules.Add(sc);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã thêm lịch học" });
        }

        // ================= 6. LỚP HỌC (CLASS) =================
        [HttpPost("classes")]
        public async Task<IActionResult> AddClass([FromBody] ClassCreateDto dto)
        {
            var c = new Class { ClassName = dto.ClassName, FacultyId = dto.FacultyId };
            _context.Classes.Add(c);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm lớp thành công" });
        }

        // ================= 7. ĐIỂM SỐ (SCORE) =================
        [HttpPost("scores")]
        public async Task<IActionResult> AddScore([FromBody] ScoreCreateDto dto)
        {
            var s = new Score
            {
                Value = dto.Value,
                StudentId = dto.StudentId,
                SubjectId = dto.SubjectId
            };
            _context.Scores.Add(s);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Nhập điểm thành công!" });
        }

        // ... Các hàm GET và Reset Password giữ nguyên như cũ ...
    }
}