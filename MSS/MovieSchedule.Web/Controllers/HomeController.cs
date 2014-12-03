using System.Web.Mvc;

namespace MovieSchedule.Web.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            if (!User.Identity.IsAuthenticated)
                return View();
            return RedirectToAction("Dashboard", "Schedule");
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            return View();
        }
    }
}
