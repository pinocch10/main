using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using MovieSchedule.Data;
using MovieSchedule.Web.Models;

namespace MovieSchedule.Web.Controllers
{
    public class ScheduleController : Controller
    {
        //
        // GET: /Schedule/

        public ActionResult Index()
        {
            List<Movie> movies = new List<Movie>();
            List<Distributor> distributors = new List<Distributor>();
            using (var model = new MovieScheduleStatsEntities())
            {
                //movies = model.Movies.Include("Distributors").ToList();
                distributors = model.Distributors.ToList();
            }
            return View(new ScheduleModel()
            {
                Movies = movies,
                Distributors = distributors
            })
            ;
        }

        public ActionResult Movie(int id, string sessionDate)
        {
            var data = new MovieScheduleModel();

            DateTime date = DateTime.Now;
            DateTime.TryParseExact(sessionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            using (var model = new MovieScheduleStatsEntities())
            {
                var movie = model.Movies.Include(x => x.Distributors).FirstOrDefault(x => x.Id == id);

                var showtimes = model.Showtimes.Include(x => x.Cinema).Include(x => x.Cinema.City).Where(x => x.MovieId == id && x.Date == date.Date).OrderBy(x => x.Cinema.City.Name).ThenBy(x => x.Cinema.Name).ToList();

                data.MovieTitle = GetMovieTitle(movie);
                data.Date = date;
                data.DistributorName = String.Join("; ", movie.Distributors.Select(x => x.Name).ToList());
                data.MovieReleaseDate = movie.ReleaseDate;
                data.Showtimes = showtimes.Select(x => new ShowtimeModel
                {
                    CinemaName = x.Cinema.Name,
                    ShowtimesCount = x.SessionsCount,
                    Showtimes = x.Sessions,
                    City = x.Cinema.City.Name,
                    Week = ((int)(data.Date - movie.ReleaseDate.Date).TotalDays) / 7
                }).ToList();
            }

            return View(data);
        }

        public JsonResult GetMovieShowtimeDates(int movieId)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var dates = model.Showtimes.Where(x => x.MovieId == movieId).Select(x => x.Date).Distinct().ToList();
                var list = dates.Select(date => date.ToString("yyyy-MM-dd")).Select(dateString =>
                    new SelectListItem { Text = dateString, Value = dateString }).ToList();
                return Json(new SelectList(list, "Value", "Text"));
            }
        }

        public JsonResult GetDistributorMovies(int distributorId)
        {
            var idString = distributorId.ToString();
            var cachedItem = HttpContext.Cache.Get(idString);
            if (cachedItem != null)
                return (JsonResult)cachedItem;
            using (var model = new MovieScheduleStatsEntities())
            {
                var movies = model.Movies.Where(x => x.Distributors.Any(xx => xx.Id == distributorId));
                var list = new List<SelectListItem>();
                foreach (var movie in movies)
                {
                    list.Add(new SelectListItem
                    {
                        Text = GetMovieTitle(movie),
                        Value = movie.Id.ToString()
                    });
                }
                var result = Json(new SelectList(list, "Value", "Text"));
                HttpContext.Cache.Add(idString, result, null, DateTime.Now.AddHours(1), TimeSpan.Zero,
                    CacheItemPriority.Normal, OnRemoveCallback);
                return result;
            }

        }

        private static string GetMovieTitle(Movie movie)
        {
            return string.IsNullOrWhiteSpace(movie.OriginalTitle) ?
                movie.Title : string.Format("{0} / {1}", movie.Title, movie.OriginalTitle);
        }

        private void OnRemoveCallback(string key, object value, CacheItemRemovedReason reason)
        {
            throw new NotImplementedException();
        }
    }
}
