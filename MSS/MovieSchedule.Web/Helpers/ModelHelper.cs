using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web;
using MovieSchedule.Data;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Web.Controllers;
using MovieSchedule.Web.Models;
using MovieSchedule.Web.Security;

namespace MovieSchedule.Web.Helpers
{
    public class ModelHelper
    {
        public static MovieScheduleModel GetMovieScheduleModel(FilterShowtimes filter)
        {
            var data = new MovieScheduleModel();

            if (filter.FederalDistrictId != 0)
            {
                data.SelectedFederalDistrict = new FederalDistrict { Id = filter.FederalDistrictId };
                filter.CityId = 0;
                filter.CinemaId = 0;
            }
            else if (filter.CityId != 0)
            {
                data.SelectedCity = new City { Id = filter.CityId };
                filter.CinemaId = 0;
            }
            else if (filter.CinemaNetworkId != 0)
            {
                data.SelectedCinemaNetwork = new CinemaNetwork { Id = filter.CinemaNetworkId };
            }
            else if (filter.CinemaId != 0)
            {
                data.SelectedCinema = new Cinema { Id = filter.CinemaId };
            }

            DateTime date = DateTime.Now;
            DateTime.TryParseExact(filter.SessionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out date);

            using (var model = new MovieScheduleStatsEntities())
            {
                var movie = model.Movies.Include(x => x.Distributors).FirstOrDefault(x => x.Id == filter.MovieId);

                var showtimes =
                    model.Showtimes
                        .Include(x => x.TargetSite)
                        .Include(x => x.Cinema)
                        .Include(x => x.Cinema.City)
                        .Include(x => x.Cinema.City.FederalDistrict)
                        .Where(x =>
                                    x.Cinema.Enabled
                                    && x.MovieId == filter.MovieId
                                    && x.Date == date.Date
                                    && (filter.FederalDistrictId == 0 || x.Cinema.City.FederalDistrictId == filter.FederalDistrictId)
                                    && (filter.CityId == 0 || x.Cinema.CityId == filter.CityId)
                                    && (filter.CinemaId == 0 || x.CinemaId == filter.CinemaId)
                                    && (filter.CinemaNetworkId == 0 || (x.Cinema.CinemaNetworkId.HasValue && x.Cinema.CinemaNetworkId == filter.CinemaNetworkId))
                                    && x.Cinema.City.CountryId == filter.CountryId)
                        .OrderBy(x => x.Cinema.City.Name)
                        .ThenBy(x => x.Cinema.Name)
                        .ToList();


                var countries =
                    model.Showtimes.Where(x =>
                        x.Cinema.Enabled
                        && x.MovieId == filter.MovieId
                        && x.Date == date.Date)
                        .Select(x => x.Cinema.City.Country)
                        .Distinct()
                        .OrderBy(x => x.Name)
                        .ToList();

                var allShowtimes =
                    model.Showtimes.Include(x => x.Cinema)
                        .Include(x => x.Cinema.City)
                        .Include(x => x.Cinema.City.FederalDistrict)
                        .Where(x =>
                            x.Cinema.Enabled
                            && x.MovieId == filter.MovieId
                            && x.Date == date.Date
                            && x.Cinema.City.CountryId == filter.CountryId)
                        .OrderBy(x => x.Cinema.City.Name)
                        .ThenBy(x => x.Cinema.Name)
                        .ToList();

                if (data.SelectedFederalDistrict != null)
                {
                    data.SelectedFederalDistrict = model.FederalDistricts.FirstOrDefault(x => x.Id == data.SelectedFederalDistrict.Id);
                }

                if (data.SelectedCity != null)
                {
                    data.SelectedCity = model.Cities.FirstOrDefault(x => x.Id == data.SelectedCity.Id);
                    if (data.SelectedCity != null)
                        data.SelectedFederalDistrict = data.SelectedCity.FederalDistrict;
                }

                if (data.SelectedCinemaNetwork != null)
                {
                    data.SelectedCinemaNetwork = model.CinemaNetworks.FirstOrDefault(x => x.Id == data.SelectedCinemaNetwork.Id);
                }

                if (data.SelectedCinema != null)
                {
                    data.SelectedCinema = model.Cinemas.FirstOrDefault(x => x.Id == data.SelectedCinema.Id);
                    if (data.SelectedCinema != null)
                        data.SelectedCity = data.SelectedCinema.City;
                    if (data.SelectedCity != null)
                        data.SelectedFederalDistrict = data.SelectedCity.FederalDistrict;
                }

                data.MovieTitle = GetMovieTitle(movie);
                if (movie != null)
                {
                    data.Title = movie.Title;
                    data.OriginalTitle = movie.OriginalTitle;
                    data.DistributorName = String.Join("; ", movie.Distributors.Select(x => x.Name).ToList());
                    data.MovieFormat = movie.Format;
                }
                data.Date = date;

                data.MovieReleaseDate = movie.ReleaseDate;

                //data.Showtimes = showtimes.Select(x => new Models.ShowtimeModel
                //{
                //    CinemaName = x.Cinema.Name,
                //    ShowtimesCount = x.SessionsCount,
                //    Showtimes = x.Sessions,
                //    City = x.Cinema.City.Name,
                //    Week = (((int)(data.Date - movie.ReleaseDate.Date).TotalDays) / 7) + 1
                //}).ToList();
                data.FederalDistricts =
                    allShowtimes.Where(x => x.Cinema.City.FederalDistrict != null && x.Cinema.City.FederalDistrict.Name != null)
                        .Select(x => x.Cinema.City.FederalDistrict)
                        .Distinct()
                        .OrderBy(x => x.Name)
                        .ToList();
                data.Cities =
                    allShowtimes.Where(x => x.Cinema.City != null && x.Cinema.City.Name != null)
                        .Select(x => x.Cinema.City)
                        .Distinct()
                        .OrderBy(x => x.Name)
                        .ToList();

                var usedCinemas = allShowtimes.Where(x =>
                    x.Cinema != null && x.Cinema.Name != null).Select(x => x.Cinema).Distinct().ToList();

                data.CinemaNetworks = usedCinemas.Where(x => x.CinemaNetwork != null).Select(x => x.CinemaNetwork).Distinct().OrderBy(x => x.Name).ToList();

                data.Cinemas = usedCinemas.Select(x => new CinemaModel
                {
                    Id = x.Id,
                    //DisplayName = string.Format("{0} ({1})", x.Name, x.City.Name),
                    DisplayName = x.Name.Replace("«", string.Empty).Replace("»", string.Empty),
                    //CityId = x.City.Id,
                    //CityName = x.City.Name,
                    //CinemaName = x.Name,

                }).OrderBy(x => x.DisplayName).ToList();

                var snapshots = model.GetSnapshots(filter.MovieId, date);

                //var snapshots2 = model.ShowtimeSnapshots.Where(x => x.MovieId == filter.MovieId && x.Date == date).OrderByDescending(x => x.Date).ToList();

                var groupped = (showtimes.GroupBy(s => new
                {
                    s.SessionsFormat,
                    s.MovieId,
                    s.Movie.Title,
                    s.Cinema.City.CountryId,
                    CountryName = s.Cinema.City.Country.Name,
                    s.CinemaId,
                    CinemaName = s.Cinema.Name,
                    s.Cinema.CityId,
                    CityName = s.Cinema.City.Name,
                    s.Date,
                })).Select(
                    x => new GrouppedShowtime
                    {
                        Format = x.Key.SessionsFormat,
                        CinemaId = x.Key.CinemaId,
                        CinemaName = x.Key.CinemaName,
                        CountryId = x.Key.CountryId,
                        CountryName = x.Key.CountryName,
                        CityId = x.Key.CityId,
                        CityName = x.Key.CityName,
                        MovieId = x.Key.MovieId,
                        MovieTitle = x.Key.Title,
                        Date = x.Key.Date,
                        Showtimes = x.ToList()
                    }).ToList();

                data.Showtimes = groupped.Select<GrouppedShowtime, ShowtimeModel>(x => GetShowtimeModel(x, data, movie, snapshots)).ToList();

                data.Countries = countries;

                PrioritizeFormats(data);

                if (date <= DateTime.Today && date.DayOfWeek == DayOfWeek.Thursday)
                {
                    var offCinemas = GetSwitchedOffCinemas(filter.MovieId, date);
                    data.OffCinemas = Enumerable.Select(offCinemas, x => new CinemaModel
                            {
                                Id = x.Id,
                                CinemaName = x.Name,
                                CityId = x.CityId,
                                CityName = x.City.Name
                            }).OrderBy(x => x.CityName).ThenBy(x => x.CinemaName).ToList();
                }
                data.ShowtimesCount = data.Showtimes.Sum(x => x.SessionsCount);
            }
            return data;
        }

        public static void PrioritizeFormats(MovieScheduleModel data)
        {
            var gg = data.Showtimes.GroupBy(g => new
            {
                g.City,
                g.CinemaName
            }).Where(g => g.Count() > 1).ToList();

            foreach (var g in gg)
            {
                var items = g.Where(x => x.Format != "2D").ToList();
                foreach (var item in items)
                {
                    var d2 = data.Showtimes.FirstOrDefault(x => x.Format == "2D"
                                                                && x.CinemaName == item.CinemaName
                                                                && x.City == item.City);
                    var sht = new Showtime() { SessionsRaw = d2.Sessions };
                    var shtMaster = new Showtime() { SessionsRaw = item.Sessions };
                    sht.SessionsCollection = sht.SessionsCollection.Except(shtMaster.SessionsCollection).ToList();

                    if (sht.SessionsCount == 0)
                    {
                        data.Showtimes.Remove(d2);
                    }
                    else
                    {
                        d2.Sessions = sht.SessionsRaw;
                        d2.SessionsCount = sht.SessionsCount;
                    }
                }
            }
        }

        public static List<Cinema> GetSwitchedOffCinemas(int movieId, DateTime date)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var yesterday = date.AddDays(-1);
                var upcoming = date.AddDays(3);

                var yesterdayShowtimeCinemas = (from s in model.Showtimes
                                                where s.Date == yesterday && s.MovieId == movieId
                                                      && s.Cinema.City.CountryId == 1
                                                select s.CinemaId).Distinct();

                var todayShowtimeCinemas = (from s in model.Showtimes
                                            where s.Date == date && s.MovieId == movieId
                                                  && s.Cinema.City.CountryId == 1
                                            select s.CinemaId).Distinct();

                var upcomingShowtimeCinemas = (from s in model.Showtimes
                                               where s.Date > date && s.Date < upcoming && s.MovieId == movieId
                                                     && s.Cinema.City.CountryId == 1
                                               select s.CinemaId).Distinct();

                var cinemas = (from cnm in model.Cinemas
                               where cnm.City.CountryId == 1
                                     && !todayShowtimeCinemas.Contains(cnm.Id)
                                     && yesterdayShowtimeCinemas.Contains(cnm.Id)
                                     && !upcomingShowtimeCinemas.Contains(cnm.Id)
                               select cnm).Include(x => x.City).Distinct().ToList();

                return cinemas;
            }
        }

        public static ShowtimeModel GetShowtimeModel(GrouppedShowtime x, MovieScheduleModel data, Movie movie, List<Showtime> snapshots)
        {
            Showtime selectedSnapshot = null;
            //if (x.Date.DayOfWeek != DayOfWeek.Thursday)
            {
                if (x.Date <= DateTime.Today)
                {
                    var selectedSnapshots = snapshots.Where(xx =>
                        xx.CinemaId == x.CinemaId
                        && xx.Date == x.Date.AddDays(-1)
                        && x.Showtimes.Select(xxx => xxx.SessionsFormat).Contains(xx.SessionsFormat)
                        ).ToList();
                    if (selectedSnapshots.Count > 0)
                        selectedSnapshot = ParsingHelper.GetIntersectedSessions(selectedSnapshots);
                }
            }
            Showtime intersectedSessions = ParsingHelper.GetIntersectedSessions(x.Showtimes);
            var result = new ShowtimeModel
            {
                CinemaName = x.CinemaName,
                City = x.CityName,
                SessionsCount = intersectedSessions.SessionsCount,//x.Showtimes.Sum(xx => xx.SessionsCount),
                Sessions = intersectedSessions.SessionsRaw, //String.Join(";", x.Showtimes.Select(xx => xx.Sessions)),
                RealFormat = x.Format,
                Format = x.Format == "TwoD" ? "2D" : x.Format == "ThreeD" ? "3D" : x.Format,
                Week = (((int)(data.Date - movie.ReleaseDate.Date).TotalDays) / 7) + 1,
                IsSnapshotPresent = selectedSnapshot != null,
            };

            if (result.IsSnapshotPresent)
            {
                result.Deviation = GetDeviation(selectedSnapshot, x, result.SessionsCount, result);
            }

            return result;
        }

        public static int GetDeviation(Showtime snapshot, GrouppedShowtime x, int showtimesSessions, ShowtimeModel sht)
        {
            if (snapshot == null) return 0;
            var result = showtimesSessions - snapshot.SessionsCount;
            if (result > 0) sht.SnapshotSessions = snapshot.SessionsRaw;

            return result;
        }

        public static string GetMovieTitle(Movie movie)
        {
            return String.IsNullOrWhiteSpace(movie.OriginalTitle) ?
                movie.Title : String.Format("{0} / {1}", movie.Title, movie.OriginalTitle);
        }
    }
}