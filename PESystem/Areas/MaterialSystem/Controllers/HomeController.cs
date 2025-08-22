using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.MaterialSystem.Controllers
{
    [Area("MaterialSystem")]
    //[Authorize(Policy = "MaterialSystemAccess")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
