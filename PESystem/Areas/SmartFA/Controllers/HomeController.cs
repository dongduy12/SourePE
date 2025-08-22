using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.SmartFA.Controllers
{
    [Area("SmartFA")]
    //[Authorize(Policy = "SmartFAAccess")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return PartialView(); // Return full view for direct navigation
        }
    }
}
