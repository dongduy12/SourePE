using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Allpart.Controllers
{
    public class YRDCLCController : Controller
    {
        [Area("Allpart")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
