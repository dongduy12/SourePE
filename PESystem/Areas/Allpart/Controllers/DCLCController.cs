using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Allpart.Controllers
{
    public class DCLCController : Controller
    {
        [Area("Allpart")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
