using API_WEB.Dtos.Auth;
using API_WEB.ModelsDB;
using API_WEB.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly CSDL_NE _context;
        public AuthController(CSDL_NE context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null)
                return Unauthorized("Invalid username or password");
            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.Password, dto.Password);
            if (result != PasswordVerificationResult.Success)
                return Unauthorized("Invalid username or password");

            return Ok(new
            {
                user.UserId,
                user.Username,
                user.FullName,
                user.Email,
                user.Role,
                user.Department,
                user.AllowedAreas
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null) return NotFound("User not found");
            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.Password, dto.OldPassword);
            if (result != PasswordVerificationResult.Success) return BadRequest("Old password incorrect");
            user.Password = hasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();
            return Ok("Password changed");
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound("User not found");
            var otp = new Random().Next(100000, 999999).ToString();
            OtpStore.SetOtp(user.Email, otp);
            await MailHelper.SendEmailAsync(user.Email, "OTP Reset Password", $"Your OTP is: {otp}");
            return Ok("OTP sent");
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound("User not found");
            if (!OtpStore.ValidateOtp(user.Email, dto.Otp)) return BadRequest("Invalid OTP");
            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();
            OtpStore.RemoveOtp(user.Email);
            return Ok("Password reset successful");
        }
    }
}
