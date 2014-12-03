using System.Dynamic;
using MovieSchedule.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MovieSchedule.Web.Models.User;

namespace MovieSchedule.Web.Models
{
    public class ScheduleModel
    {
        public UserModel User { get; set; }
        public List<Movie> Movies { get; set; }
        public List<DistributorModel> Distributors { get; set; }

        private List<MovieModel> _allMovies = new List<MovieModel>();

        public List<MovieModel> AllMovies
        {
            get { return _allMovies; }
            set { _allMovies = value; }
        }

    }

    public class DistributorModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
    }
}
