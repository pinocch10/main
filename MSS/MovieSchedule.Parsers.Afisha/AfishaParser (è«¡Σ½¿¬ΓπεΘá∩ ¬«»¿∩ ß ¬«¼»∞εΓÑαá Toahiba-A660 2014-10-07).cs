using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.Afisha
{
    public class AfishaParser : IMovieScheduleParser, ILoggable
    {
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

        public const string TargetSite = "afisha.ru";
        public static readonly string[] CititesToOmit = new string[] { "Киев", "Харьков", "Донецк" };
        private const int PerspectiveDays = 6;

        #endregion

        #region IMovieScheduleParser implemenataion

        public Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public string GetTargetSite()
        {
            return TargetSite;
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
                sources = model.Sources.Include("City").Where(x => x.TargetSite == TargetSite && x.City != null && x.Cinema == null && x.Movie == null).ToList();
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
                cities = model.Cities.Include(x => x.Sources).Where(x => x.Sources.Any(xx => xx.TargetSite == TargetSite && xx.MovieId == null && xx.CinemaId == null)).ToList();
            }
            DateTime parseDate = DateTime.Today;

            for (int i = 0; i < PerspectiveDays; i++)
            {
                foreach (var city in cities)
                {
                    ParseShowTimeForCityForDate(city, parseDate.AddDays(i));
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
                if (CititesToOmit.Contains(cityLink.Text)) continue;
                using (var model = new MovieScheduleStatsEntities())
                {
                    if (CititesToOmit.Contains(cityLink.Text)) continue;
                    var dbCity = model.Cities.Include("Sources").FirstOrDefault(x => x.Name == cityLink.Text);
                    if (dbCity != null)
                    {
                        if (!dbCity.Sources.Any(x => x.TargetSite == TargetSite && x.URL == cityLink.Reference.ToString()))
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
                    city.Sources.First(x => x.TargetSite == TargetSite && x.MovieId == null && x.CinemaId == null);

                var url = string.Format(URLs.ShowtimesFormat, citySource.Parameter, parseDate);
                var cityUri = new Uri(url);
                var response = HttpSender.GetHtmlResponse(url, encoding: this.Encoding);

                //TODO Add logging or something, to indicate that there was a problem. Probably can be added to HttpSender itself
                if (!response.Success)
                    return;

                var doc = new HtmlDocument();
                doc.LoadHtml(response.Content);
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
                        Cinema cinema;

                        movie = model.Movies.FirstOrDefault(x => x.Title == movieLink.Text);
                        if (movie == null) continue;

                        var movieRows = movieNode.NextSibling.NextSibling.SelectNodes("tbody/tr");
                        Showtime showtime = null;
                        foreach (var movieRow in movieRows)
                        {
                            var cells = movieRow.SelectNodes("td");
                            var cinemaLink = cells[0].ParseLink();
                            var sessions = cells[1].GetNodes("div/div/span", "div/span");
                            var sessionsformat = ParseFormat(cells[1]);
                            if (cinemaLink == null && showtime != null)
                            {
                                showtime.SessionsFormat = sessionsformat.ToString();
                                CompleteShowtime(showtime, sessions, model);
                            }
                            else
                            {

                                cinemaLink.Text = cinemaLink.Text.ReplaceBadChar();

                                City replacementCity = this.TryGetReplacementCity(cinemaLink, model, cityUri) ??
                                                       ParsingHelper.TryGetReplacementCity(cinemaLink, model);

                                var cityToUse = replacementCity ?? city;

                                var originalCity = city.Name;

                                cinema = ParsingHelper.GetCinema(cinemaLink, cityToUse, TargetSite, model, originalCity, GenerateSourceForCinema);

                                showtime = new Showtime
                                {
                                    TargetSiteId = TargetSite.GetTargetSite().Id,
                                    Movie = movie,
                                    MovieId = movie.Id,
                                    Cinema = cinema,
                                    CinemaId = cinema.Id,
                                    Date = parseDate,
                                    CreationDate = DateTime.Now,
                                    SessionsFormat = sessionsformat.ToString()
                                };

                                CompleteShowtime(showtime, sessions, model);
                            }
                        }
                        model.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    this.GetDefaultLogger().Fatal("Failed to parse afisha", ex);
                    this.GetLogger("ErrorLoggerAfisha").Error(response.Content);
                }
            }
        }

        private static SessionFormat ParseFormat(HtmlNode movieRow)
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
                        return SessionFormat.ThreeD;
                    case "movie-in-3D m-4dx-3d":
                        return SessionFormat.FourDX;
                    default:
                        return SessionFormat.TwoD;
                }
            }
            return SessionFormat.TwoD;
        }

        private static void CompleteShowtime(Showtime showtime, HtmlNodeCollection sessions, MovieScheduleStatsEntities model)
        {
            ParsingHelper.PopulateSessions(showtime, sessions);

            showtime.Additional = string.Empty;
            showtime.URL = string.Empty;

            showtime = ParsingHelper.CheckShowtimePresent(showtime, model);
            if (showtime != null)
                model.Showtimes.Add(showtime);
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
                TargetSite = TargetSite,
                TargetSiteId = TargetSite.GetTargetSite().Id,
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
                    TargetSite = TargetSite,
                    TargetSiteId = TargetSite.GetTargetSite().Id,
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
                sources = model.Sources.Include("City").Where(x => x.TargetSite == TargetSite && x.City != null && x.Cinema == null && x.Movie == null).ToList();
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

        private void ParseShowtimesForCity(City city)
        {


        }

        #endregion
    }
}
