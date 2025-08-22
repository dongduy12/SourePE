using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class ConvertController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
