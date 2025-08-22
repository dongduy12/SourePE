using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PESystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using API_WEB.Dtos.Auth;

namespace PESystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _clientFactory;

        public AccountController(ApplicationDbContext context, IHttpClientFactory clientFactory)
        {
            _context = context;
            _clientFactory = clientFactory;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var client = _clientFactory.CreateClient("ApiClient");
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync("api/Auth/login", new LoginDto
                {
                    Username = model.Username,
                    Password = model.Password
                });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Không thể kết nối đến máy chủ.");
                return View(model);
            }

            if (response.IsSuccessStatusCode)
            {
                var apiUser = await response.Content.ReadFromJsonAsync<ApiUser>();
                if (apiUser != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, apiUser.UserId.ToString()),
                        new Claim(ClaimTypes.Name, apiUser.Username),
                        new Claim(ClaimTypes.Role, apiUser.Role),
                        new Claim("AllowedAreas", apiUser.AllowedAreas ?? string.Empty),
                        new Claim("FullName", apiUser.FullName),
                        new Claim("Email", apiUser.Email),
                        new Claim("Department", apiUser.Department)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProps = new AuthenticationProperties { IsPersistent = model.RememberMe };
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProps);
                    return RedirectToAction("Home", "Home");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ModelState.AddModelError(string.Empty, "Username hoặc Password không đúng.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Đăng nhập thất bại.");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                var userExists = await _context.Users.AnyAsync(u => u.Username == model.Username);
                if (userExists)
                {
                    ModelState.AddModelError("", "Username đã tồn tại.");
                    return View(model);
                }

                var hasher = new PasswordHasher<User>();
                var user = new User
                {
                    Username = model.Username,
                    Password = hasher.HashPassword(null, model.Password),
                    FullName = model.FullName,
                    Email = model.Email,
                    Department = model.Department,  
                    Role = "User",
                    AllowedAreas = new List<string> {""} // Giá trị mặc định
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return RedirectToAction("Login");
            }

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var username = User.Identity?.Name;
            if (username == null) return RedirectToAction("Login");

            var client = _clientFactory.CreateClient("ApiClient");
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync("api/Auth/change-password", new ChangePasswordDto
                {
                    Username = username,
                    OldPassword = model.OldPassword,
                    NewPassword = model.NewPassword
                });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Không thể kết nối đến máy chủ.");
                return View(model);
            }

            if (response.IsSuccessStatusCode)
            {
                ViewBag.Message = "Password changed";
                return View();
            }

            ModelState.AddModelError(string.Empty, "Failed to change password");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _clientFactory.CreateClient("ApiClient");
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync("api/Auth/forgot-password", new ForgotPasswordDto { Email = model.Email });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Không thể kết nối đến máy chủ.");
                return View(model);
            }
            if (response.IsSuccessStatusCode)
            {
                ViewBag.Message = "OTP sent";
                return View();
            }
            ModelState.AddModelError(string.Empty, "Failed to send OTP");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _clientFactory.CreateClient("ApiClient");
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync("api/Auth/reset-password", new ResetPasswordDto
                {
                    Email = model.Email,
                    Otp = model.Otp,
                    NewPassword = model.NewPassword
                });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Không thể kết nối đến máy chủ.");
                return View(model);
            }
            if (response.IsSuccessStatusCode)
            {
                ViewBag.Message = "Password reset successful";
                return View();
            }
            ModelState.AddModelError(string.Empty, "Failed to reset password");
            return View(model);
        }


        //Khi username không có quyền truy cập, sẽ điều hướng đến đây!
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); //Xóa xác thực người dùng( xóa session)

            //// Xóa cache để ngăn người dùng quay lại trang trước sau khi đăng xuất
            //Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            //Response.Headers["Pragma"] = "no-cache";
            //Response.Headers["Expires"] = "0";

            // Xóa cookie sau khi đăng xuất
            Response.Cookies.Delete(".AspNetCore.Cookies");
            return RedirectToAction("Login");
        }
    }

    public class ApiUser
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? AllowedAreas { get; set; }
    }
}
