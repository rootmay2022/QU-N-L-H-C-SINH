namespace ConnectDB.DTO
{
    public class ScheduleCreateDto
    {
        public DateTime LearnDate { get; set; }
        public int Slot { get; set; }
        public string Room { get; set; } = string.Empty;
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public int ClassId { get; set; }
        public string? Note { get; set; }
    }
}