using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.CheckList.Controllers
{
    public class HomeController : Controller
    {
        [Area("CheckList")]
        [Authorize(Policy = "CheckListAccess")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
