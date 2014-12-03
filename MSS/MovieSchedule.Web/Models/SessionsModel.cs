using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MovieSchedule.Data;

namespace MovieSchedule.Web.Models
{
    public class SessionsModel
    {
        public Distributor Distributor { get; set; }
        public List<Movie> Movies { get; set; }
        public List<DateTime> MovieDates { get; set; }
    }
}