using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MovieSchedule.Web.Controllers;
using MovieSchedule.Web.Helpers;

namespace MovieSchedule.Tests.Controllers
{
    [TestClass]
    public class ScheduleControllerTest
    {
        [TestMethod]
        public void Schedule()
        {
            var controler = new ScheduleController();
            var result = controler.Export(0, "");
        }

        [TestMethod]
        public void SwitchedOff()
        {
            ModelHelper.GetSwitchedOffCinemas(3054, new DateTime(2014, 11, 13));
        }
    }
}
