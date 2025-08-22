using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class Handover : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
