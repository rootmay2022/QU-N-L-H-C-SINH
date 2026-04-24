#pragma warning disable CS8602 // Bịt miệng lỗi Dereference of possibly null
#pragma warning disable CS8618 // Bịt miệng lỗi Non-nullable property
#pragma warning disable CS8629 // Bịt miệng lỗi Nullable value type

using ConnectDB.Data;
using ConnectDB.Models;
// using ConnectDB.DTO; // Mở comment dòng này nếu mày có thư mục DTO riêng
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConnectDB.Controllers
{
    [Authorize(Roles = "Admin,Teacher")]
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
        public async Task<IActionResult> GetSubjects()
        {
            var subjects = await _context.Subjects
                .Select(s => new
                {
                    s.Id,
                    s.SubjectName,
                    s.Credits,
                    s.FacultyId,
                    FacultyName = _context.Faculties
                                    .Where(f => f.Id == s.FacultyId)
                                    .Select(f => f.FacultyName)
                                    .FirstOrDefault() ?? "Chưa phân khoa"
                })
                .ToListAsync();

            return Ok(subjects);
        }

        [HttpPost("subjects")]
        public async Task<IActionResult> AddSubject([FromBody] SubjectCreateDto dto)
        {
            var s = new Subject
            {
                SubjectName = dto.SubjectName,
                Credits = dto.Credits,
                FacultyId = dto.FacultyId
            };
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
        [HttpGet("students")]
        public async Task<IActionResult> GetStudents()
        {
            var students = await _context.Students
                .Include(s => s.Class)
                .Select(s => new
                {
                    Id = s.Id,
                    StudentCode = s.StudentCode,
                    StudentId = s.StudentCode,
                    FullName = s.FullName,
                    Email = s.Email,
                    ClassId = s.ClassId,
                    ClassName = s.Class != null ? s.Class.ClassName : "Chưa xếp lớp"
                })
                .ToListAsync();

            return Ok(students);
        }

        [HttpPost("students")]
        public async Task<IActionResult> AddStudent([FromBody] SyncStudent_Req dto)
        {
            var maSV = dto.GetCode();

            if (string.IsNullOrWhiteSpace(maSV))
                return BadRequest(new { message = "Lỗi Backend: Không nhận được Mã SV từ Web gửi lên!" });

            if (await _context.Users.AnyAsync(u => u.Username == maSV))
                return BadRequest(new { message = "Mã sinh viên đã tồn tại!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Username = maSV,
                    Password = "123",
                    Role = "Student"
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var s = new Student
                {
                    StudentCode = maSV,
                    FullName = dto.FullName ?? string.Empty,
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

        [HttpPut("students/{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] SyncStudent_Req dto)
        {
            var st = await _context.Students.FindAsync(id);
            if (st == null) return NotFound(new { message = "Không tìm thấy sinh viên!" });

            var maSV = dto.GetCode();
            if (string.IsNullOrWhiteSpace(maSV))
                return BadRequest(new { message = "Lỗi Backend: Mã SV trống!" });

            if (st.StudentCode != maSV)
            {
                bool exists = await _context.Users.AnyAsync(u => u.Username == maSV);
                if (exists) return BadRequest(new { message = "Mã sinh viên mới này đã tồn tại trong hệ thống!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(st.UserId);
                if (user != null)
                {
                    user.Username = maSV;
                    _context.Users.Update(user);
                }

                st.StudentCode = maSV;
                st.FullName = dto.FullName ?? string.Empty;
                st.Birthday = dto.Birthday;
                st.Gender = dto.Gender;
                st.Phone = dto.Phone;
                st.Email = dto.Email;
                st.Address = dto.Address;
                st.ClassId = dto.ClassId;

                _context.Students.Update(st);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Cập nhật thông tin thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Lỗi server: " + ex.Message);
            }
        }

        // ================= 4. GIẢNG VIÊN (TEACHER) =================
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers() => Ok(await _context.Teachers.ToListAsync());

        [HttpPost("teachers")]
        public async Task<IActionResult> AddTeacher([FromBody] CreateTeacherDto dto)
        {
            string username = string.IsNullOrWhiteSpace(dto.Username)
                ? (dto.Email != null ? dto.Email.Split('@')[0] : "gv_" + DateTime.Now.ToString("mmss"))
                : dto.Username;

            if (await _context.Users.AnyAsync(u => u.Username == username))
                return BadRequest(new { message = $"Tài khoản '{username}' đã tồn tại!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Username = username,
                    Password = dto.Password ?? "123",
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

                return Ok(new { message = $"Thêm thành công! Username: {username}" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "Lỗi Backend: " + ex.Message });
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
            return Ok(new { message = "Xóa thành công" });
        }


        // ================= 5. LỊCH HỌC (SCHEDULE) =================
        [HttpGet("schedules")]
        public async Task<IActionResult> GetSchedules([FromQuery] int? classId = null)
        {
            var query = _context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.Teacher)
                .Include(s => s.Class)
                .AsQueryable();

            if (classId.HasValue) query = query.Where(s => s.ClassId == classId.Value);

            var result = await query
                .Select(s => new
                {
                    s.Id,
                    Học_Ngày = s.LearnDate,
                    Ca_Học = s.Slot,
                    Phòng = s.Room,
                    Môn_Học = s.Subject != null ? s.Subject.SubjectName : "---",
                    Giảng_Viên = s.Teacher != null ? s.Teacher.FullName : "---",
                    Lớp = s.Class != null ? s.Class.ClassName : "---",
                    Ghi_Chú = s.Note
                })
                .OrderBy(s => s.Học_Ngày)
                .ToListAsync();

            return Ok(result);
        }

        // ================= 6. LỚP HỌC (CLASS) =================
        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses() => Ok(await _context.Classes.Include(c => c.Faculty).ToListAsync());

        [HttpPost("classes")]
        public async Task<IActionResult> AddClass([FromBody] ClassCreateDto dto)
        {
            var c = new Class { ClassName = dto.ClassName, FacultyId = dto.FacultyId };
            _context.Classes.Add(c);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm lớp thành công" });
        }

        // ================= 7. ĐIỂM SỐ (SCORE) =================
        [HttpGet("scores")]
        public async Task<IActionResult> GetAllScores()
        {
            var result = await _context.Scores
                .Include(s => s.Student)
                .Include(s => s.Subject)
                .Select(s => new
                {
                    s.Id,
                    studentId = s.StudentId,
                    subjectId = s.SubjectId,
                    studentName = s.Student != null ? s.Student.FullName : "---",
                    subjectName = s.Subject != null ? s.Subject.SubjectName : "---",
                    kT1 = s.KT1,
                    kT2 = s.KT2,
                    diemThi = s.DiemThi,
                    diemTrungBinh = s.DiemTrungBinh,
                    ketQua = s.KetQua,
                    credits = s.Subject != null ? s.Subject.Credits : 0
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpPost("scores")]
        public async Task<IActionResult> UpdateScore([FromBody] ScoreDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Dữ liệu trống!" });
            try
            {
                var score = await _context.Scores
                    .FirstOrDefaultAsync(s => s.StudentId == dto.StudentId && s.SubjectId == dto.SubjectId);

                if (score == null)
                {
                    score = new Score { StudentId = dto.StudentId, SubjectId = dto.SubjectId };
                    _context.Scores.Add(score);
                }

                score.KT1 = dto.KT1;
                score.KT2 = dto.KT2;
                score.DiemThi = dto.DiemThi;
                score.DiemTrungBinh = (float)Math.Round((score.KT1 + score.KT2 + score.DiemThi * 2) / 4, 1);
                score.KetQua = score.DiemTrungBinh >= 5 ? "Qua môn" : "Học lại";

                await _context.SaveChangesAsync();
                return Ok(new { message = "Lưu điểm thành công!", dtb = score.DiemTrungBinh, ketQua = score.KetQua });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi Database!", detail = ex.Message });
            }
        }

        // ================= 8. HỆ THỐNG TÀI KHOẢN (USERS) =================
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    Id = u.Id,
                    Username = u.Username,
                    Role = u.Role,
                    FacultyName = u.Role == "Student"
                        ? _context.Students.Where(s => s.UserId == u.Id).Select(s => s.Class.Faculty.FacultyName).FirstOrDefault()
                        : "---"
                })
                .ToListAsync();
            return Ok(users);
        }

        [HttpPut("users/reset-password/{id}")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.Password = "123";
            await _context.SaveChangesAsync();
            return Ok(new { message = "Mật khẩu đã reset về 123" });
        }

        [HttpPost("create-user-manual")]
        public async Task<IActionResult> CreateUserManual([FromBody] User u)
        {
            if (await _context.Users.AnyAsync(x => x.Username == u.Username))
                return BadRequest(new { message = "Username đã tồn tại!" });
            u.Password = u.Password ?? "123";
            _context.Users.Add(u);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Tạo tài khoản thành công!" });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa tài khoản!" });
        }

        [HttpPut("users/{id}/password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ReactChangePassReq dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản!" });
            user.Password = dto.NewPassword;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }

        // ================= 9. QUẢN LÝ NGHỈ PHÉP (LEAVE REQUESTS) - ĐÃ FIX CHUẨN =================

        // Xem danh sách đơn nghỉ phép (Có check NULL an toàn để không văng 500)
        [HttpGet("leave-requests")]
        public async Task<IActionResult> GetLeaveRequests()
        {
            try
            {
                var requests = await _context.LeaveRequests
                    .Include(r => r.Teacher).ThenInclude(t => t.User)
                    // SỬA DÒNG NÀY: Thay r.CreatedAt thành r.Id
                    .OrderByDescending(r => r.Id)
                    .Select(r => new
                    {
                        Id = r.Id,
                        TeacherName = (r.Teacher != null && r.Teacher.User != null) ? r.Teacher.User.FullName : "GV không xác định",
                        OffDate = r.OffDate,
                        Reason = r.Reason,
                        Status = r.Status
                    })
                    .ToListAsync();
                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi Fetch: {ex.Message}");
            }
        }

        // Duyệt đơn nghỉ phép
        [HttpPut("leave-requests/approve/{id}")]
        public async Task<IActionResult> ApproveLeaveRequest(int id)
        {
            try
            {
                var request = await _context.LeaveRequests.FindAsync(id);
                if (request == null) return NotFound(new { message = "Không tìm thấy đơn ID: " + id });

                request.Status = "Approved";
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã duyệt đơn thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi xử lý duyệt: {ex.Message}");
            }
        }

        // Từ chối đơn nghỉ phép
        [HttpPut("leave-requests/reject/{id}")]
        public async Task<IActionResult> RejectLeaveRequest(int id)
        {
            try
            {
                var request = await _context.LeaveRequests.FindAsync(id);
                if (request == null) return NotFound(new { message = "Không tìm thấy đơn ID: " + id });

                request.Status = "Rejected";
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã từ chối đơn!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi xử lý từ chối: {ex.Message}");
            }
        }

    } // HẾT PHẦN CONTROLLER CHÍNH - BẮT ĐẦU PHẦN DTO

    // ================= DTO CLASSES CẦN THIẾT ĐỂ KHÔNG BỊ BÁO LỖI =================

    public class FacultyDto { public string FacultyName { get; set; } }

    public class SubjectCreateDto { public string SubjectName { get; set; } public int Credits { get; set; } public int FacultyId { get; set; } }

    public class ClassCreateDto { public string ClassName { get; set; } public int FacultyId { get; set; } }

    public class CreateTeacherDto { public string? Username { get; set; } public string? Password { get; set; } public string FullName { get; set; } public string? Email { get; set; } }

    public class SyncStudent_Req
    {
        public string? StudentCode { get; set; }
        public string? StudentId { get; set; }
        public string? FullName { get; set; }
        public DateTime Birthday { get; set; }
        public string? Gender { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int ClassId { get; set; }
        public string GetCode() => !string.IsNullOrWhiteSpace(StudentCode) ? StudentCode : (StudentId ?? string.Empty);
    }

    // ĐÂY LÀ THẰNG SCOREDTO MÀ MÀY BỊ BÁO LỖI NÈ
    public class ScoreDto
    {
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public float KT1 { get; set; }
        public float KT2 { get; set; }
        public float DiemThi { get; set; }
    }

    public class ReactChangePassReq { public string NewPassword { get; set; } }
}