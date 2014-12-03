using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using MoreLinq;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Interfaces;
using MovieSchedule.Data;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Web.Filters;
using MovieSchedule.Web.Helpers;
using MovieSchedule.Web.Models;
using MovieSchedule.Web.Security;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using WebGrease.Css.Ast.Selectors;

namespace MovieSchedule.Web.Controllers
{
    public class ScheduleController : Controller
    {
        //
        // GET: /Schedule/
        public ActionResult Index()
        {

            //JsonConvert.SerializeObject(new FilterShowtimes(){}, JsonDataType)

            List<MovieModel> movies = new List<MovieModel>();
            List<DistributorModel> distributors = new List<DistributorModel>();
            using (var model = new MovieScheduleStatsEntities())
            {
                var date = DateTime.Today;
                movies = GetAllMovies(model, date);

                distributors = model.Distributors
                 .Where(x => x.Movies.Any(xx => xx.Showtimes.Count(xxx => xxx.Date >= date) > 0))
                 .Distinct().OrderBy(x => x.Name)
                 .Select(x => new DistributorModel { Id = x.Id, DisplayName = x.DisplayName ?? x.Name }).OrderBy(x => x.DisplayName).ToList();
            }
            var user = new MovieScheduleStatsEntities().GetUserById(User.Identity.Name);
            return View(new ScheduleModel
            {
                User = user == null ? null : user.ToModel(),
                AllMovies = movies,
                Distributors = distributors
            });
        }

        private static List<MovieModel> GetAllMovies(MovieScheduleStatsEntities model, DateTime date)
        {
            var movies =
                model.Movies.Include("Distributors")
                    .Where(x => x.Showtimes.Any(xxx => xxx.Date >= date) && x.Distributors.Any())
                    .Select(
                        x =>
                            new MovieModel
                            {
                                MovieId = x.Id,
                                MovieTitle = x.Title,
                                MovieOriginalTitle = x.OriginalTitle,
                                DistributorId = x.Distributors.FirstOrDefault().Id,
                                DistributorTitle = x.Distributors.FirstOrDefault().Name,
                            }).OrderBy(x => x.DistributorTitle).ThenBy(x => x.MovieTitle)
                    .ToList();

            movies.ForEach(x =>
            {
                x.DisplayTitle = string.Format("{0} - {1}", x.DistributorTitle,
                    string.IsNullOrWhiteSpace(x.MovieOriginalTitle)
                        ? x.MovieTitle
                        : string.Format("{0} / {1}", x.MovieTitle, x.MovieOriginalTitle));

                x.CombinedId = string.Format("{0}-{1}", x.DistributorId, x.MovieId);
            });
            return movies;
        }

        public ActionResult Movie(int id, string sessionDate)
        {
            var data = new Models.MovieScheduleModel();

            DateTime date = DateTime.Now;
            DateTime.TryParseExact(sessionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            using (var model = new MovieScheduleStatsEntities())
            {
                var movie = model.Movies.Include(x => x.Distributors).FirstOrDefault(x => x.Id == id);

                var showtimes = model.Showtimes.Include(x => x.Cinema).Include(x => x.Cinema.City).Where(x => x.MovieId == id && x.Date == date.Date).OrderBy(x => x.Cinema.City.Name).ThenBy(x => x.Cinema.Name).ToList();

                data.MovieTitle = ModelHelper.GetMovieTitle(movie);
                data.Date = date;
                data.DistributorName = String.Join("; ", movie.Distributors.Select(x => x.Name).ToList());
                data.MovieReleaseDate = movie.ReleaseDate;
                data.Showtimes = showtimes.Select(x => new Models.ShowtimeModel
                {
                    CinemaName = x.Cinema.Name,
                    SessionsCount = x.SessionsCount,
                    Sessions = x.SessionsRaw,
                    City = x.Cinema.City.Name,
                    Week = (((int)(data.Date - movie.ReleaseDate.Date).TotalDays) / 7) + 1
                }).ToList();
            }

            return View(data);
        }

        [System.Web.Mvc.Authorize]
        public ActionResult Dashboard()
        {
            return Index();
        }

        public ActionResult Test(string date)
        {
            return View();
        }

        [JsonFilter(Param = "filter", JsonDataType = typeof(FilterShowtimes))]
        public ActionResult Showtimes(FilterShowtimes filter)
        {
            var data = ModelHelper.GetMovieScheduleModel(filter);
            return View(data);
        }

        [JsonFilter(Param = "filter", JsonDataType = typeof(FilterShowtimesCompare))]
        public ActionResult CompareShowtimes(FilterShowtimesCompare filter)
        {
            var leftFilter = filter;
            var rightFilter = new FilterShowtimes()
            {
                MovieId = filter.SecondMovieId,
                SessionDate = filter.SessionDate,
                CountryId = filter.CountryId
            };
            var leftSessions = ModelHelper.GetMovieScheduleModel(leftFilter);
            var rightSessions = ModelHelper.GetMovieScheduleModel(rightFilter);

            var comparison = new ComparisonModel { Left = leftSessions, Right = rightSessions };

            foreach (var leftShowtime in leftSessions.Showtimes)
            {
                comparison.Showtimes.Add(new ComparisonShowtime
                {
                    LeftShowtime = leftShowtime,
                    RightShowtime = rightSessions.Showtimes.FirstOrDefault(x =>
                        x.CinemaName == leftShowtime.CinemaName &&
                        x.City == leftShowtime.City &&
                        x.Format == leftShowtime.Format)
                });
            }

            foreach (var rightShowtime in rightSessions.Showtimes.Where(x => !leftSessions.Showtimes.Any(xx => xx.CinemaName == x.CinemaName && xx.City == x.City && xx.Format == x.Format)))
            {
                comparison.Showtimes.Add(new ComparisonShowtime
                {
                    LeftShowtime = null,
                    RightShowtime = rightShowtime
                });
            }


            return View(comparison);
        }


        public ActionResult GetSwitchedOffCinemas(int movieId, string sessionDate)
        {
            DateTime date = DateTime.Now;
            DateTime.TryParseExact(sessionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            var cinemas = ModelHelper.GetSwitchedOffCinemas(movieId, date);

            var model = cinemas.Select(
                x => new CinemaModel
                {
                    Id = x.Id,
                    CinemaName = x.Name,
                    CityId = x.CityId,
                    CityName = x.City.Name
                }).ToList();

            return View(model);
        }


        public ActionResult Compare()
        {
            var compareModel = new CompareModel();
            using (var model = new MovieScheduleStatsEntities())
            {
                var movies = GetAllMovies(model, DateTime.Today);
                compareModel.AllMovies = movies;
            }

            return View(compareModel);
        }

        public FileContentResult Export(
           int movieId,
           string sessionDate,
           int federalDistrictId = 0,
           int cityId = 0,
           int cinemaId = 0,
           int cinemaNetworkId = 0,
           int countryId = 1)
        {

            var filter = new FilterShowtimes()
            {
                MovieId = movieId,
                CinemaId = cinemaId,
                SessionDate = sessionDate,
                FederalDistrictId = federalDistrictId,
                CityId = cityId,
                CinemaNetworkId = cinemaNetworkId,
                CountryId = countryId
            };

            var data = ModelHelper.GetMovieScheduleModel(filter);

            using (ExcelPackage package = new ExcelPackage())
            //using (ExcelPackage package = new ExcelPackage(new FileInfo(@"c:\Temp\t.xlsx")))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Дневной экспорт");
                //Set up movie header
                worksheet.Cells[1, 1].Value = "Дистрибьютор";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 2].Value = data.DistributorName;

                var filterTitles = new StringBuilder();

                if (federalDistrictId != 0)
                {
                    PopuateFilterHeader(worksheet, "Федеральный округ", data.SelectedFederalDistrict.Name);
                    filterTitles.AppendFormat("_ФО_{0}", data.SelectedFederalDistrict.Name);
                }

                if (cityId != 0)
                {
                    PopuateFilterHeader(worksheet, "Город", data.SelectedCity.Name);
                    filterTitles.AppendFormat("_{0}", data.SelectedCity.Name);
                }

                if (cinemaId != 0)
                {
                    PopuateFilterHeader(worksheet, "Кинотеатр", data.SelectedCinema.Name);
                    filterTitles.AppendFormat("_{0}", data.SelectedCinema.Name);
                }

                if (cinemaNetworkId != 0)
                {
                    PopuateFilterHeader(worksheet, "Сеть кинотеатров", data.SelectedCinemaNetwork.Name);
                    filterTitles.AppendFormat("_сеть_{0}", data.SelectedCinemaNetwork.Name);
                }

                worksheet.Cells[2, 1].Value = "Название фильма";
                worksheet.Cells[2, 1].Style.Font.Bold = true;
                worksheet.Cells[2, 2].Value = data.MovieTitle;

                worksheet.Cells[3, 1].Value = "Дата";
                worksheet.Cells[3, 1].Style.Font.Bold = true;
                worksheet.Cells[3, 2].Value = data.Date.ToShortDateString();

                //Set up table header
                worksheet.Row(4).Style.Font.Bold = true;
                worksheet.Row(4).Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Row(4).Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                worksheet.Cells[4, 1].Value = "Город";
                worksheet.Cells[4, 2].Value = "Кинотеатр";
                worksheet.Cells[4, 3].Value = "Формат";
                worksheet.Cells[4, 4].Value = "Кол-во сеансов";
                worksheet.Cells[4, 5].Value = "Время сеансов";
                worksheet.Cells[4, 6].Value = "Отклонение";
                bool positiveDeviationPresent;
                bool negativeDeviationPresent;
                for (int i = 0; i < data.Showtimes.Count; i++)
                {
                    var showtime = data.Showtimes[i];
                    //shift cells according to the current shift and make it 1 based
                    int rowIndex = i + 1 + 4;
                    worksheet.Cells[rowIndex, 1].Value = showtime.City;
                    worksheet.Cells[rowIndex, 2].Value = showtime.CinemaName;
                    worksheet.Cells[rowIndex, 3].Value = showtime.Format;
                    worksheet.Cells[rowIndex, 4].Value = showtime.SessionsCount;
                    worksheet.Cells[rowIndex, 5].Value = showtime.Sessions;

                    SetDeviationValue(worksheet, showtime.Deviation, rowIndex, 6);
                }
                int summaryRow = data.Showtimes.Count + 1 + 4;
                worksheet.Row(summaryRow).Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Row(summaryRow).Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                worksheet.Cells[summaryRow, 4].Value = string.Format("Всего сеансов:{0}", data.Showtimes.Sum(x => x.SessionsCount));

                var positive = data.Showtimes.Where(x => x.Deviation > 0).ToArray();
                var negative = data.Showtimes.Where(x => x.Deviation < 0).ToArray();

                if (positive.Any())
                {
                    SetDeviationValue(worksheet, positive.Sum(x => x.Deviation), summaryRow, 6);
                }
                if (negative.Any())
                {
                    SetDeviationValue(worksheet, negative.Sum(x => x.Deviation), summaryRow + 1, 6);
                }

                worksheet.Cells.AutoFitColumns();

                return File(package.GetAsByteArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", string.Format("{0}_{1}{2}.xlsx", data.MovieTitle, sessionDate, filterTitles.ToString()).RemoveForbiddenSymbols());

                //return new ExcelResult("Test.xlsx", package.Workbook.WorkbookXml.InnerXml);
            }
        }




        private static void SetDeviationValue(ExcelWorksheet worksheet, int value, int rowIndex, int colIndex)
        {
            worksheet.Cells[rowIndex, colIndex].Style.Font.Bold = true;
            worksheet.Cells[rowIndex, colIndex].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            if (value > 0)
            {
                worksheet.Cells[rowIndex, colIndex].Value = "+" + value;
                worksheet.Cells[rowIndex, colIndex].Style.Font.Color.SetColor(Color.ForestGreen);
            }
            if (value < 0)
            {
                worksheet.Cells[rowIndex, colIndex].Value = value;
                worksheet.Cells[rowIndex, colIndex].Style.Font.Color.SetColor(Color.Red);
            }
        }

        private static void PopuateFilterHeader(ExcelWorksheet worksheet, string filterTitle, string filterValue, int row = 1, int startingCell = 3)
        {
            worksheet.Cells[row, startingCell].Value = filterTitle;
            worksheet.Cells[row, startingCell].Style.Font.Bold = true;
            worksheet.Cells[row, startingCell + 1].Value = filterValue;
        }

        /// <summary>
        /// This method gets showtimes using view, but it is really slow compared to LINQ grouping used
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sessionDate"></param>
        /// <returns></returns>
        public ActionResult GetShowtimesUsingView(int id, string sessionDate)
        {
            throw new NotImplementedException();
            //var data = new MovieScheduleModel();

            //DateTime date = DateTime.Now;
            //DateTime.TryParseExact(sessionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            //using (var model = new MovieScheduleStatsEntities())
            //{
            //    var movie = model.Movies.Include(x => x.Distributors).FirstOrDefault(x => x.Id == id);

            //    var showtimes = model.vShowtimes.Where(x => x.MovieId == id && x.Date == date).OrderBy(x => x.City).ThenBy(x => x.Cinema).ToList();

            //    data.MovieTitle = ModelHelper.GetMovieTitle(movie);
            //    data.Date = date;
            //    data.DistributorName = String.Join("; ", movie.Distributors.Select(x => x.Name).ToList());
            //    data.MovieReleaseDate = movie.ReleaseDate;
            //    data.Showtimes = showtimes.Select(x => new Models.ShowtimeModel
            //    {
            //        CinemaName = x.Cinema,
            //        SessionsCount = x.Sessions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length,
            //        Sessions = x.Sessions,
            //        City = x.City,
            //        Week = (((int)(data.Date - movie.ReleaseDate.Date).TotalDays) / 7) + 1
            //    }).ToList();
            //}

            //return View(data);
        }

        public JsonResult GetFederalDistricts()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var federalDistricts = model.FederalDistricts.ToList();
                var list = federalDistricts.Select(x =>
                    new SelectListItem { Text = x.Name, Value = x.Id.ToString() }).ToList();
                return Json(new SelectList(list, "Value", "Text"));
            }
        }

        public JsonResult GetMovieShowtimeDates(int movieId)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var dates = model.Showtimes.Where(x => x.MovieId == movieId).Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
                var list = dates.Select(date => date.ToString("yyyy-MM-dd")).Select(dateString =>
                    new SelectListItem { Text = dateString, Value = dateString }).ToList();
                return Json(new SelectList(list, "Value", "Text"));
            }
        }

        [JsonFilter(Param = "movieIds", JsonDataType = typeof(int[]))]
        public JsonResult GetMoviesShowtimeDates(int[] movieIds)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var dates = model.Showtimes.Where(x => movieIds.Contains(x.MovieId)).Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
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
            var date = DateTime.Today;
            using (var model = new MovieScheduleStatsEntities())
            {
                var movies = model.Movies.Where(x =>
                    x.Distributors.Any(xx => xx.Id == distributorId)
                    && x.Showtimes.Count(xx => xx.Date >= date) > 0
                    ).OrderBy(x => x.Title).ToList();
                var list = new List<SelectListItem>();
                foreach (var movie in movies)
                {
                    list.Add(new SelectListItem
                    {
                        Text = ModelHelper.GetMovieTitle(movie),
                        Value = movie.Id.ToString()
                    });
                }
                var result = Json(new SelectList(list, "Value", "Text"));
                HttpContext.Cache.Add(idString, result, null, DateTime.Now.AddHours(1), TimeSpan.Zero,
                    CacheItemPriority.Normal, OnRemoveCallback);
                return result;
            }
        }

        private void OnRemoveCallback(string key, object value, CacheItemRemovedReason reason)
        {
            throw new NotImplementedException();
        }
    }
}
