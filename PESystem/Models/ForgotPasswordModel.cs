using System.ComponentModel.DataAnnotations;

namespace PESystem.Models
{
    public class ForgotPasswordModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
