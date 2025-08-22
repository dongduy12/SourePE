using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace PESystem.Areas.DataCloud.Controllers
{
    [Area("DataCloud")]
    [Authorize(Policy = "DataCloudAccess")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
