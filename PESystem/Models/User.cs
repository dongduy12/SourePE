using System;
using System.Collections.Generic;

namespace PESystem.Models
{
    public partial class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public List<string> AllowedAreas { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Department { get; set; } = null!;
    }
}
