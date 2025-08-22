using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace PESystem.Areas.BiHass.Controllers
{
    [Area("HassBi")]
    [Authorize(Policy = "HassBiAccess")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
