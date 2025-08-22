using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using PESystem.Models;
using Microsoft.AspNetCore.Identity;

namespace PESystem.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        public AdminController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // GET: api/Admin/GetUsers
        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.FullName,
                        u.Role,
                        u.AllowedAreas,
                        u.Email,
                        u.Department
                    })
                    .ToListAsync();

                if (users == null || !users.Any())
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi lấy danh sách người dùng: {ex.Message}" });
            }
        }

        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto userDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { success = false, message = "Dữ liệu đầu vào không hợp lệ.", errors });
            }

            try
            {
                // Kiểm tra username trùng lặp
                if (await _context.Users.AnyAsync(u => u.Username == userDto.Username))
                {
                    return BadRequest(new { success = false, message = "Username đã tồn tại." });
                }

                // Kiểm tra độ dài mật khẩu
                if (userDto.Password.Length < 8)
                {
                    return BadRequest(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự." });
                }

                // Tạo đối tượng User
                var user = new User
                {
                    Username = userDto.Username,
                    Role = userDto.Role,
                    AllowedAreas = userDto.AllowedAreas,
                    FullName = userDto.FullName,
                    Email = userDto.Email,
                    Department = userDto.Department
                };

                // Mã hóa mật khẩu
                user.Password = _passwordHasher.HashPassword(user, userDto.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = new { user.UserId, user.Username, user.FullName, user.Role, user.AllowedAreas, user.Email, user.Department } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi thêm người dùng: {ex.Message}" });
            }
        }


        // POST: api/Admin/EditUser
        [HttpPost("EditUser")]
        public async Task<IActionResult> EditUser([FromBody] UserEditDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu đầu vào không hợp lệ.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            try
            {
                var user = await _context.Users.FindAsync(userDto.UserId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                user.FullName = userDto.FullName;
                user.AllowedAreas = userDto.AllowedAreas;
                user.Role = userDto.Role;
                user.Department = userDto.Department;
                user.Email = userDto.Email;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = new { user.UserId, user.Username, user.FullName, user.Role, user.AllowedAreas, user.Email, user.Department } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi sửa người dùng: {ex.Message}" });
            }
        }

        // DELETE: api/Admin/DeleteUser/{id}
        [HttpDelete("DeleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa người dùng thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi xóa người dùng: {ex.Message}" });
            }
        }
    }

    // DTO cho tạo người dùng
    public class UserCreateDto
    {
        [Required(ErrorMessage = "Username là bắt buộc.")]
        public string Username { get; set; } = null!;

        public string? Password { get; set; }

        [Required(ErrorMessage = "Role là bắt buộc.")]
        public string Role { get; set; } = null!;

        [Required(ErrorMessage = "AllowedAreas là bắt buộc.")]
        public List<string> AllowedAreas { get; set; } = null!;

        [Required(ErrorMessage = "FullName là bắt buộc.")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email là bắt buộc.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Department là bắt buộc.")]
        public string Department { get; set; } = null!;
    }

    // DTO cho sửa người dùng
    public class UserEditDto
    {
        [Required(ErrorMessage = "UserId là bắt buộc.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "FullName là bắt buộc.")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Role là bắt buộc.")]
        public string Role { get; set; } = null!;

        [Required(ErrorMessage = "AllowedAreas là bắt buộc.")]
        public List<string> AllowedAreas { get; set; } = null!;

        [Required(ErrorMessage = "Email là bắt buộc.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Department là bắt buộc.")]
        public string Department { get; set; } = null!;
    }
}