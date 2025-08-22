using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    public class SearchController : Controller
    {
        public IActionResult Index()
        {
            return PartialView(); // Return full view for direct navigation
        }
    }
}
