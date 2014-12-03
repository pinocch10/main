using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MovieSchedule.Core;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.Afisha
{
    public class AfishaParser : IMovieScheduleParser, ILoggable
    {
        #region Constructors

        public AfishaParser()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                TargetSite = model.TargetSites.FirstOrDefault(x => x.Shortcut == TargetSiteShortcut);
            }
        }

        #endregion

        #region URLs
        private class URLs
        {
            public const string CinemasFormat = "http://www.afisha.ru/{0}/cinemas/cinema_list/{1}";
            public const string ShowtimesFormat = "http://www.afisha.ru/{0}schedule_cinema/{1:dd-MM-yyyy}/";
            public const string Base = "http://www.afisha.ru/";
            public const string Base2 = "http://www.afisha.ru/msk/concerts/";
        }
        #endregion

        #region Constants

        public const string TargetSiteShortcut = "afisha.ru";
        public static readonly string[] CititesToOmit = new string[] { "Киев", "Харьков", "Донецк" };
        private const int PerspectiveDays = 3;

        #endregion

        #region IMovieScheduleParser implemenataion

        public TargetSite TargetSite { get; private set; }

        public Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public string GetTargetSite()
        {
            return TargetSiteShortcut;
        }

        public string GetBaseCityUrl()
        {
            return URLs.Base;
        }

        public Dictionary<string, List<Link>> ParseCinemas()
        {
            throw new NotImplementedException();
            var sw = new Stopwatch();
            sw.Start();
            var sources = new List<Source>();
            using (var model = new MovieScheduleStatsEntities())
            {
                sources = model.Sources.Include("City").Where(x => x.TargetSite == TargetSiteShortcut && x.City != null && x.Cinema == null && x.Movie == null).ToList();
            }

            var result = new StringBuilder();
            int pagesCount = 0;
            foreach (var source in sources)
            {
                pagesCount++;
                //result.AppendLine(city.Name);
                try
                {
                    var cityParameter = source.Parameter;
                    List<string> pages;
                    List<string> pages2;
                    string baseUrl = string.Format(URLs.CinemasFormat, cityParameter, string.Empty);
                    ParseCinemasInternal(baseUrl, source.City, result, out pages);

                    foreach (var page in pages)
                    {
                        ParseCinemasInternal(page, source.City, result, out pages2);
                    }


                    //Thread.Sleep(100);
                }
                catch (Exception)
                {

                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed {0} to parse {1} pages", sw.Elapsed, pagesCount);
            File.WriteAllText(@"D:\cinemas_cities_afisha.csv", result.ToString(), Encoding.Unicode);
        }

        public void ParseShowtimes()
        {
            var cities = new List<City>();
            using (var model = new MovieScheduleStatsEntities())
            {
                cities = model.Cities.Include(x => x.Sources).Include(x => x.Satellites).Where(x => x.Sources.Any(xx => xx.TargetSite == TargetSiteShortcut && xx.MovieId == null && xx.CinemaId == null)).OrderBy(x => x.Name).ToList();
                //cities = cities.Where(x => x.Name == "Владикавказ").ToList();
            }
            DateTime parseDate = DateTime.Today;

            for (int i = 0; i < PerspectiveDays; i++)
            {
                foreach (var city in cities)
                {
                    DateTime parseDateOffset = parseDate.AddDays(i);
                    this.GetDefaultLogger().InfoFormat("afisha. Parsing {0}({1}) {2:yyyy-MM-dd}", city.Name, city.Id, parseDateOffset);
                    ParseShowTimeForCityForDate(city, parseDateOffset);
                }
            }
        }

        public void ParseCities()
        {
            var citiesSource = HttpSender.GetHtmlResponse(URLs.Base2, encoding: this.Encoding);
            var doc = new HtmlDocument();
            doc.LoadHtml(citiesSource.Content);
            var root = doc.DocumentNode;
            //var cityLinks = root.SelectNodes("//div[@class='choose_city']/table/tr/td/a");
            var cityLinkNodes = root.SelectNodes("//span[@class='s-dropdown afh-dd-city']/ul[@class='g-fl-left afh-dd-city-ul']/li/a");
            foreach (var cityLinkNode in cityLinkNodes)
            {
                var cityLink = cityLinkNode.ParseLink();

                if (cityLink.Text == "Петербург") cityLink.Text = "Санкт-Петербург";
                cityLink.Text = cityLink.Text.Replace("ё", "е");
                cityLink.Text = cityLink.Text.Replace("Ё", "Е");
                if (CititesToOmit.Contains(cityLink.Text)) continue;
                using (var model = new MovieScheduleStatsEntities())
                {
                    if (CititesToOmit.Contains(cityLink.Text)) continue;
                    var dbCity = model.Cities.Include("Sources").FirstOrDefault(x => x.Name == cityLink.Text);
                    if (dbCity != null)
                    {
                        if (!dbCity.Sources.Any(x => x.TargetSite == TargetSiteShortcut && x.URL == cityLink.Reference.ToString()))
                        {
                            var source = GenerateSourceForCity(dbCity, cityLink.Reference.Segments[1], cityLink.Reference.ToString());
                            dbCity.Sources.Add(source);
                        }
                    }
                    else
                    {
                        var country = ParsingHelper.GetCountry(model);
                        var city = new City
                        {
                            Name = cityLink.Text,
                            Country = country
                        };
                        var source = GenerateSourceForCity(city, cityLink.Reference.Segments[1], cityLink.Reference.ToString());
                        city.Sources.Add(source);
                        model.Cities.Add(city);
                    }
                    model.SaveChanges();
                }
            }
        }

        public void Parse()
        {
            var sw = new Stopwatch();
            sw.Start();
            ParseCities();
            sw.Stop();
            Console.WriteLine("Cities parsed in {0}", sw.Elapsed);
            sw.Restart();
            ParseShowtimes();
            sw.Stop();
            Console.WriteLine("Showtimes parsed in {0}", sw.Elapsed);
        }

        #endregion

        #region Private methods

        private void ParseShowTimeForCityForDate(City city, DateTime parseDate)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var citySource =
                    city.Sources.First(x => x.TargetSite == TargetSiteShortcut && x.MovieId == null && x.CinemaId == null);

                var url = string.Format(URLs.ShowtimesFormat, citySource.Parameter, parseDate);
                var cityUri = new Uri(url);
                var showtimesSource = HttpSender.GetHtmlResponse(url, encoding: this.Encoding);

                var doc = new HtmlDocument();
                doc.LoadHtml(showtimesSource.Content);
                var root = doc.DocumentNode;
                try
                {
                    //var dateDropDown = root.SelectSingleNode("//div[@class='m-schedule-top-mrg']/div[@class='m-float-left']/select/option[@selected='selected']");

                    var movieNodes = root.SelectNodes("//div[@class='m-disp-table']");
                    if (movieNodes == null) return;
                    foreach (var movieNode in movieNodes)
                    {
                        var movieLink = movieNode.SelectSingleNode("h3").ParseLink();
                        Movie movie;
                        Cinema cinema = null;

                        movie = model.Movies.FirstOrDefault(x => x.Title == movieLink.Text);
                        if (movie == null) continue;

                        var movieRows = movieNode.NextSibling.NextSibling.SelectNodes("tbody/tr");
                        Showtime showtime = null;
                        foreach (var movieRow in movieRows)
                        {
                            var cells = movieRow.SelectNodes("td");
                            var cinemaLink = cells[0].ParseLink();
                            var sessions = cells[1].GetNodes("div/div/span", "div/span");
                            var sessionsformat = ParseFormat(cells[1], movie.Format);
                            if (cinemaLink == null && showtime != null)
                            {
                                var showtime2 = new Showtime
                                {
                                    TargetSiteId = showtime.TargetSiteId,
                                    MovieId = showtime.MovieId,
                                    CinemaId = showtime.CinemaId,
                                    Date = parseDate,
                                    CreationDate = DateTime.Now,
                                    SessionsFormat = sessionsformat.ToString(),
                                    ParseRunId = ParseRunProvider.Instance.GetParseRun().Id,
                                    CityId = city.Id
                                };

                                CompleteShowtime(showtime2, movie, cinema, sessions, model);
                                model.SaveChanges();
                            }
                            else
                            {

                                cinemaLink.Text = cinemaLink.Text.ReplaceBadChar();
                                if (ParsingHelper.SkipNotCurrentCityCinema(cinemaLink, model))
                                    continue;
                                City replacementCity = this.TryGetReplacementCity(cinemaLink, model, cityUri) ??
                                                       ParsingHelper.TryGetReplacementCity(cinemaLink, model);

                                var cityToUse = replacementCity ?? city;
                                var originalCity = city.Name;

                                cinema = ParsingHelper.GetCinema(cinemaLink, cityToUse, TargetSiteShortcut, model, originalCity, GenerateSourceForCinema);

                                showtime = new Showtime
                                {
                                    TargetSiteId = TargetSiteShortcut.GetTargetSite().Id,
                                    Movie = movie,
                                    MovieId = movie.Id,
                                    Cinema = cinema,
                                    CinemaId = cinema.Id,
                                    Date = parseDate,
                                    CreationDate = DateTime.Now,
                                    SessionsFormat = sessionsformat.ToString(),
                                    ParseRunId = ParseRunProvider.Instance.GetParseRun().Id,
                                    CityId = city.Id
                                };

                                CompleteShowtime(showtime, movie, cinema, sessions, model);
                                model.SaveChanges();
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    this.GetDefaultLogger().Fatal("Failed to parse afisha", ex);
                    this.GetLogger("ErrorLoggerAfisha").Error(showtimesSource.Content);
                }
            }
        }

        private static SessionFormat ParseFormat(HtmlNode movieRow, string movieFormat = "")
        {
            var formatCell = movieRow.SelectSingleNode("div[@class='line']/div");
            if (formatCell != null && formatCell.Attributes.Contains("class"))
            {
                switch (formatCell.Attributes["class"].Value)
                {
                    case "movie-in-3D m-imax-2d":
                    case "movie-in-3D m-imax-3d":
                        return SessionFormat.IMAX;
                    case "movie-in-3D":
                        return movieFormat == "2D" ? SessionFormat.TwoD : SessionFormat.ThreeD;
                    case "movie-in-3D m-4dx-3d":
                        return SessionFormat.FourDX;
                    default:
                        return SessionFormat.TwoD;
                }
            }
            return SessionFormat.TwoD;
        }

        private void CompleteShowtime(Showtime showtime, Movie movie, Cinema cinema, HtmlNodeCollection sessions, MovieScheduleStatsEntities model)
        {
            ParsingHelper.PopulateSessions(showtime, sessions);

            showtime.Additional = string.Empty;
            showtime.URL = string.Empty;
            var date = showtime.Date;
            var format = showtime.SessionsFormat;
            showtime = ParsingHelper.CheckShowtimePresent(showtime, model);
            if (showtime != null)
            {
                model.Showtimes.Add(showtime);
            }
            ParsingHelper.TryAddSnapshot(this.TargetSite, model, movie, cinema, date, showtime, format.ToString());
        }

        private void ParseCinemasInternal(string url, City city, StringBuilder result, out List<string> pages)
        {
            var response = HttpSender.GetHtmlResponse(url, encoding: this.Encoding);
            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);
            var root = doc.DocumentNode;

            var names = root.SelectNodes("//div[@class='places-list-item']/h3/a[@id]");

            foreach (var name in names)
            {
                result.AppendLine(string.Format("{0},{1}", city.Name, NodeHelper.TrimDecode(name.InnerText)));
            }

            var pageLinks = root.SelectNodes("//ul[@class='page-list']/li/a");
            pages = new List<string>();
            if (pageLinks != null)
                pages.AddRange(pageLinks.Select(pageLink => pageLink.Attributes["href"].Value));
        }

        private static Source GenerateSourceForCinema(Cinema cinema, City city, Link cinemaLink, string originalCity)
        {
            var source = new Source
            {
                Cinema = cinema,
                CityId = city.Id,
                TargetSite = TargetSiteShortcut,
                TargetSiteId = TargetSiteShortcut.GetTargetSite().Id,
                Parameter = cinemaLink.Reference.ToString(),
                CreationDate = DateTime.Now,
                URL = cinemaLink.Reference.ToString(),
                OriginalCity = originalCity,
                Text = cinemaLink.Text
            };
            return source;
        }

        public static Source GenerateSourceForCity(City dbCity, string cityId, string url)
        {
            var source = new Source
                {
                    City = dbCity,
                    Parameter = cityId,
                    URL = url,
                    TargetSite = TargetSiteShortcut,
                    TargetSiteId = TargetSiteShortcut.GetTargetSite().Id,
                    CreationDate = DateTime.Now
                };
            return source;
        }

        #endregion

        #region Old methods

        public void ParseCinemasOld()
        {
            var sw = new Stopwatch();
            sw.Start();
            var sources = new List<Source>();
            using (var model = new MovieScheduleStatsEntities())
            {
                sources = model.Sources.Include("City").Where(x => x.TargetSite == TargetSiteShortcut && x.City != null && x.Cinema == null && x.Movie == null).ToList();
            }

            var result = new StringBuilder();
            int pagesCount = 0;
            foreach (var source in sources)
            {
                pagesCount++;
                //result.AppendLine(city.Name);
                try
                {
                    var cityParameter = source.Parameter;
                    List<string> pages;
                    List<string> pages2;
                    string baseUrl = string.Format(URLs.CinemasFormat, cityParameter, string.Empty);
                    ParseCinemasInternal(baseUrl, source.City, result, out pages);

                    foreach (var page in pages)
                    {
                        ParseCinemasInternal(page, source.City, result, out pages2);
                    }


                    //Thread.Sleep(100);
                }
                catch (Exception)
                {

                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed {0} to parse {1} pages", sw.Elapsed, pagesCount);
            File.WriteAllText(@"D:\cinemas_cities_afisha.csv", result.ToString(), Encoding.Unicode);
        }

        #endregion
    }
}
