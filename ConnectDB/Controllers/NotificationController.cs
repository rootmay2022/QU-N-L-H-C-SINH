using ConnectDB.Data;
using ConnectDB.DTO;
using ConnectDB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Đã thêm: Quan trọng nhất
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _context;
    public NotificationController(AppDbContext context) { _context = context; }

    // 1. API GỬI THÔNG BÁO
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(roleClaim))
        {
            return Unauthorized("Không xác định được người gửi!");
        }

        int? finalTargetId = dto.TargetId;

        // 🚀 BẮT ĐẦU ĐOẠN FIX LỖI "LỆCH PHA ID" Ở ĐÂY
        // Nếu gửi cho một Giảng viên cụ thể, phải dịch TeacherId -> UserId
        if (dto.TargetType == "Teacher" && dto.TargetId != null && dto.TargetId > 0)
        {
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == dto.TargetId);
            if (teacher != null)
            {
                finalTargetId = teacher.UserId; // Chuyển đổi sang UserId để GV có thể đọc được
            }
        }
        // 🚀 KẾT THÚC ĐOẠN FIX

        var notification = new Notification
        {
            SenderId = int.Parse(userIdClaim),
            SenderRole = roleClaim,
            TargetType = dto.TargetType,
            TargetId = finalTargetId, // Sử dụng ID đã được "dịch"
            ClassId = dto.ClassId,
            Title = dto.Title ?? "Không có tiêu đề",
            Content = dto.Content ?? "",
            Type = dto.Type ?? "Note",
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Gửi thành công!" });
    }

    // 2. API LẤY THÔNG BÁO (Fix lỗi đỏ ở ToListAsync và Null)
    [HttpGet("my-notifications")]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(roleClaim))
            return Unauthorized("Vui lòng đăng nhập!");

        int userId = int.Parse(userIdClaim);
        string role = roleClaim;

        var notifications = await _context.Notifications
            .Where(n =>
                n.TargetType == "All" || // 1. Thông báo Toàn trường (Ai cũng thấy)
                (n.TargetType == role && (n.TargetId == userId || n.TargetId == 0 || n.TargetId == null)) || // 2. Gửi riêng cho mình HOẶC gửi cho tất cả người cùng Role (GV/SV)
                (role == "Student" && n.TargetType == "Class" && _context.Students.Any(s => s.UserId == userId && s.ClassId == n.ClassId)) // 3. Gửi riêng cho lớp (SV mới thấy)
            )
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }
}