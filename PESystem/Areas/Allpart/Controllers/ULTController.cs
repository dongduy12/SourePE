using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Allpart.Controllers
{
    public class ULTController : Controller
    {
        [Area("Allpart")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
