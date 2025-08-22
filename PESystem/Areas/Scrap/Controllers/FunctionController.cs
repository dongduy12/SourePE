using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Scrap.Controllers
{
    [Area("Scrap")]
    [Authorize(Policy = "ScrapAccess")]
    public class FunctionController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
