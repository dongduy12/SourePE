using Microsoft.AspNetCore.Mvc;

namespace API_WEB.Controllers.App
{
    public class ScanController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
