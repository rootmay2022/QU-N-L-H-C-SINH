using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ConnectDB.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        [Required]
        public string Username { get; set; } = string.Empty;
        [JsonIgnore]
        [Required]
        public string Password { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = "Student";

        public int? StudentId { get; set; }

        [JsonIgnore]
        [ForeignKey("StudentId")]
        public Student? Student { get; set; }

        public int? TeacherId { get; set; }

        [JsonIgnore]
        [ForeignKey("TeacherId")]
        public Teacher? Teacher { get; set; }
    }
}