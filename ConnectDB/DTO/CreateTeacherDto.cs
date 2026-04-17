namespace ConnectDB.DTO
{
    public class CreateTeacherDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = "123";
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}