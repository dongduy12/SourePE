using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Repositories.Controllers
{
    [Area("Repositories")]
    public class SearchProductController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
