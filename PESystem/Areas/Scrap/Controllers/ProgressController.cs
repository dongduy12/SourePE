using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Scrap.Controllers
{
    [Area("Scrap")]
    public class ProgressController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
