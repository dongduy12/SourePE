using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Bonepile.Controllers
{
    public class BonepileController : Controller
    {
        [Area("Bonepile")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
