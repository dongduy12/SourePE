using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class AdminViewController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
