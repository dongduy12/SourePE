using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace PESystem.Areas.Repositories.Controllers
{
    [Area("Repositories")]
    [Authorize(Policy = "RepositoriesAccess")]
    public class AddProductController : Controller
    {
        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
