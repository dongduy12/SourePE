using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Scrap.Controllers
{
    [Area("Scrap")]
    public class MRBController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
