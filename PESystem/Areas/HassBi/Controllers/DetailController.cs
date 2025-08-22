using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.HassBi.Controllers
{
    [Area("HassBi")]
    [Authorize(Policy = "HassBiAccess")]
    public class DetailController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
