using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Repositories.Controllers
{
    [Area("Repositories")]
    [Authorize(Policy = "RepositoriesAccess")]
    public class KhoOkController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
 