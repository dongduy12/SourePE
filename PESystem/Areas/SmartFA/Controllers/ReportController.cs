using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class ReportController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
