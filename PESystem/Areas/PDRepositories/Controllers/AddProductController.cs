using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.PDRepositories.Controllers
{
    [Area("PDRepositories")]
    public class AddProductController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
