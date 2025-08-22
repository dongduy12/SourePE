using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class GuideController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
