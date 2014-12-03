using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using MovieSchedule.Data;
using MovieSchedule.Web.Models;
using MovieSchedule.Web.Models.User;
using MovieSchedule.Web.Security;

namespace MovieSchedule.Web.Controllers
{
    public class UserController : Controller
    {
        //
        // GET: /User/

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult LogIn(UserModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (Membership.ValidateUser(model.Username, model.Password))
                {

                    var storedUser = new MovieScheduleStatsEntities().GetUserByUsernameAndPassword(model.Username, model.Password);


                    FormsAuthentication.SetAuthCookie(storedUser.Id.ToString(), model.RememberMe);

                    Response.SetCookie(AuthenticationHelper.SetupFormsAuthTicket(storedUser, model.RememberMe));
                    if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1 && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Dashboard", "Schedule");
                }
                ModelState.AddModelError("", "The user name or password provided is incorrect.");
            }

            return View();
        }

        public ActionResult LogIn()
        {
            return View();
        }

        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        public ActionResult Cabinet()
        {
            int userId;
            int.TryParse(User.Identity.Name, NumberStyles.Any, CultureInfo.InvariantCulture, out userId);
            using (var model = new MovieScheduleStatsEntities())
            {
                var user = model.Users.FirstOrDefault(x => x.Id == userId);
                if (user != null)
                {
                    return View(user.ToModel());
                }
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public ActionResult Cabinet(UserModel model)
        {
            int userId;
            int.TryParse(User.Identity.Name, NumberStyles.Any, CultureInfo.InvariantCulture, out userId);
            using (var db = new MovieScheduleStatsEntities())
            {
                var user = db.Users.FirstOrDefault(x => x.Id == userId);
                if (user != null)
                {
                    if (!string.IsNullOrWhiteSpace(model.Email) && !model.Email.Equals(user.Email))
                    {
                        user.Email = model.Email;
                        db.SaveChanges();
                    }
                    else
                    {
                        ViewData["Error"] = "Электронный адрес не был сохранен";
                    }
                    ModelState.Clear();
                    return View(user.ToModel());
                }
            }
            return View();
        }
    }

    public class AuthenticationHelper
    {
        public static HttpCookie SetupFormsAuthTicket(User user, bool persistanceFlag)
        {
            var authTicket = new FormsAuthenticationTicket
            (
                1,
                user.Id.ToString(),
                DateTime.Now,
                DateTime.Now.AddMinutes(30),
                persistanceFlag,
                user.Id.ToString()
            );
            var encTicket = FormsAuthentication.Encrypt(authTicket);

            return new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
        }
    }
}

