using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.DDR.Controllers
{
    [Area("DDR")]
    public class SearchController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
