namespace PESystem.Controllers
{
    internal class ResetPasswordDto
    {
        public string Email { get; set; }
        public string Otp { get; set; }
        public string NewPassword { get; set; }
    }
}