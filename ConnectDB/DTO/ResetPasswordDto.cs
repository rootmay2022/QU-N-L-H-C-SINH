namespace ConnectDB.DTO
{
    public class ResetPasswordDto
    {
        public string? Otp { get; set; }
        public string? NewPassword { get; set; }
    }
}