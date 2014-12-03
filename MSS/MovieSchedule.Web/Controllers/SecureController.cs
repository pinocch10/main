using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using MovieSchedule.Data;

namespace MovieSchedule.Web.Controllers
{
    public class AuthorizeUserAttribute : AuthorizeAttribute
    {
        private AuthorizationContext _currentContext;

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            _currentContext = filterContext;
            base.OnAuthorization(filterContext);
        }
                                                                   
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var isAuthorized = base.AuthorizeCore(httpContext);

            int userId;
            int.TryParse(httpContext.User.Identity.Name, out userId);

#if DEBUG
            userId = 1;
            isAuthorized = true;
#endif

            string controller = _currentContext.RouteData.GetRequiredString("controller");
            using (var model = new MovieScheduleStatsEntities())
            {
                var user = model.Users.FirstOrDefault(x => x.Id == userId);
                if (user == null || user.Features.All(x => x.Address != controller))
                    isAuthorized = false;
            }

            return isAuthorized;
        }

    }

    public class SecureController : Controller
    {
        //
        // GET: /Secure/
        [AuthorizeUser]
        public ActionResult Index()
        {
            return View();
        }

    }
}
