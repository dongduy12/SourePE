using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Bonepile.Controllers
{
    public class KhoB31Controller : Controller
    {
        [Area("Bonepile")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
