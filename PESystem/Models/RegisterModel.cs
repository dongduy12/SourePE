using System.ComponentModel.DataAnnotations;

namespace PESystem.Models
{
    public class RegisterModel
    {
        [Required]
        public string Username { get; set; }        
        [Required]
        public string FullName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu không khớp!")]
        public string ConfirmPassword { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Department { get; set; }
    }
}
