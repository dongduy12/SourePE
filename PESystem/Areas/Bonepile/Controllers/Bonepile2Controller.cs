using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.Bonepile.Controllers
{
    public class Bonepile2Controller : Controller
    {
        [Area("Bonepile")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
