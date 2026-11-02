using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebVanTai.Areas.TicketInspector.Controllers
{
    [Area("TicketInspector")]
    [Authorize(Roles = "TicketInspector")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

