using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class StatisticsBGAController : Controller
    {
        public IActionResult Index()
        {
            return PartialView();
        }
    }
}
