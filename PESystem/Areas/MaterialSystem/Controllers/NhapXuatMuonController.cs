using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.MaterialSystem.Controllers
{
    [Area("MaterialSystem")]
    public class NhapXuatMuonController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
