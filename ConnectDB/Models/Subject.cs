using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Thêm cái này để dùng [ForeignKey]

namespace ConnectDB.Models
{
    public class Subject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string SubjectName { get; set; } = string.Empty;

        public int Credits { get; set; }

        // --- PHẦN THÊM MỚI ĐỂ LIÊN KẾT KHOA ---
        [Required]
        public int FacultyId { get; set; } // Khóa ngoại lưu ID của Khoa

        [ForeignKey("FacultyId")]
        public virtual Faculty? Faculty { get; set; } // Navigation property để lấy thông tin Khoa khi cần
        // --------------------------------------

        // Giữ nguyên logic cũ của m ở đây, không ảnh hưởng gì nhé
        public ICollection<Score>? Scores { get; set; }
    }
}