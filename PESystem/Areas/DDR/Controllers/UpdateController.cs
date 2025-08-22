using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.DDR.Controllers
{
    [Area("DDR")]
    public class UpdateController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
