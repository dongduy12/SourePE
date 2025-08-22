using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.PDRepositories.Controllers
{
    [Area("PDRepositories")]
    public class OutProductController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
