using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PESystem.Models;
using System.Diagnostics;

namespace PESystem.Controllers
{

    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Home()
        {

            return PartialView();
        }
    }
}