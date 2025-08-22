using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Repositories.Controllers
{
    [Area("Repositories")]
    [Authorize(Policy = "RepositoriesAccess")]
    public class ExportController : Controller
    {
        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
