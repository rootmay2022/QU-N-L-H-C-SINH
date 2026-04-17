using System;

namespace ConnectDB.DTO
{
    public class StudentUpdateDto
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime Birthday { get; set; }
        public int ClassId { get; set; } // Phải có dòng này
        public string? Gender { get; set; } // Phải có dòng này
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }
}