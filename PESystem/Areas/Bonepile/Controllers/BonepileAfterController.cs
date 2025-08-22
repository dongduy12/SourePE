using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Bonepile.Controllers
{
    public class BonepileAfterController : Controller
    {
        [Area("Bonepile")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
