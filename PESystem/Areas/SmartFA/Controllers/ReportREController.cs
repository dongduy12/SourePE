using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class ReportREController : Controller
    {
        public IActionResult Index()
        {
            return PartialView();
        }
    }
}
