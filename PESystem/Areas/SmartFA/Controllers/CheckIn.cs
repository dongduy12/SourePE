using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class CheckIn : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
