using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PESystem.Areas.CheckList.Controllers
{
    [Area("CheckList")]
    [Authorize(Policy = "CheckListAccess")]
    public class ListIssueController : Controller
    {

        public IActionResult Index()
        {
            return View();
        }
    }
}
