using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Repositories.Controllers
{
    [Area("Repositories")]
    public class Bonepile2Controller : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
