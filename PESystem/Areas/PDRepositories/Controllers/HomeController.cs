using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.PDRepositories.Controllers
{
    [Area("PDRepositories")]
    //[Authorize(Policy = "PDRepositoriesAccess")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
