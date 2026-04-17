using ConnectDB.Data;
using ConnectDB.Models;
using ConnectDB.DTO; // 👈 M giữ đúng dòng này
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

        // --- 1. KHOA ---
        [HttpGet("faculties")]
        public async Task<IActionResult> GetFaculties() => Ok(await _context.Faculties.ToListAsync());

        [HttpPost("faculties")]
        public async Task<IActionResult> AddFaculty([FromBody] Faculty f)
        {
            _context.Faculties.Add(f);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm khoa thành công" });
        }

        // --- 2. MÔN HỌC ---
        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects() => Ok(await _context.Subjects.ToListAsync());

        [HttpPost("subjects")]
        public async Task<IActionResult> AddSubject([FromBody] Subject s)
        {
            _context.Subjects.Add(s);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm môn học thành công" });
        }

        // --- 3. SINH VIÊN ---
        [HttpPost("students")]
        public async Task<IActionResult> AddStudent([FromBody] ConnectDB.DTO.StudentCreateDTO dto)
        {
            // 👆 Tao viết đầy đủ "ConnectDB.DTO." để ép nó lấy đúng file trong folder DTO
            if (await _context.Users.AnyAsync(u => u.Username == dto.StudentCode))
                return BadRequest(new { message = "Mã sinh viên đã tồn tại!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Username = dto.StudentCode,
                    Password = BCrypt.Net.BCrypt.HashPassword("123"),
                    Role = "Student",
                    FullName = dto.FullName
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var s = new Student
                {
                    StudentCode = dto.StudentCode,
                    FullName = dto.FullName,
                    Birthday = dto.Birthday,
                    ClassId = dto.ClassId,
                    UserId = newUser.Id,
                    Gender = dto.Gender,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    Address = dto.Address
                };
                _context.Students.Add(s);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new { message = "Thêm sinh viên thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPut("students/{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] ConnectDB.DTO.StudentUpdateDto dto)
        {
            // 👆 Tương tự, dùng Full Name để dứt điểm lỗi "Does not contain ClassId"
            var st = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
            if (st == null) return NotFound();

            st.FullName = dto.FullName;
            st.Birthday = dto.Birthday;
            st.ClassId = dto.ClassId;
            st.Gender = dto.Gender;
            st.Phone = dto.Phone;
            st.Email = dto.Email;
            st.Address = dto.Address;

            if (st.User != null) st.User.FullName = dto.FullName;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thành công" });
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
                return BadRequest("Lỗi khi xóa");
            }
        }

        // --- 4. GIẢNG VIÊN ---
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
                return Ok(new { message = "Thêm giảng viên thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest("Lỗi: " + ex.Message);
            }
        }
    }

    public class CreateTeacherDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = "123";
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}