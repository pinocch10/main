using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{
    public class CinemaParkParser : IMovieScheduleParser, ILoggable
    {

        private TargetSite _targetSite = null;

        class URLs
        {
            public const string Base = "http://m.cinemapark.ru/";
        }

        public string TargetSiteShortcut
        {
            get { return "cinemapark.ru"; }
        }

        public string Base
        {
            get { return "http://m.cinemapark.ru/"; }
        }

        public Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public TargetSite TargetSite
        {
            get
            {
                if (_targetSite == null)
                {
                    using (var model = new MovieScheduleStatsEntities())
                    {
                        var ts = model.TargetSites.FirstOrDefault(x => x.Shortcut == TargetSiteShortcut);
                        if (ts != null)
                        {
                            _targetSite = ts;
                        }
                        else
                        {
                            _targetSite = new TargetSite
                            {
                                Shortcut = TargetSiteShortcut,

                            };
                            model.TargetSites.Add(_targetSite);
                            model.SaveChanges();
                        }
                    }
                }
                return _targetSite;
            }
        }

        public string GetTargetSite()
        {
            return TargetSiteShortcut;
        }

        public string GetBaseCityUrl()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, List<Link>> ParseCinemas()
        {
            throw new NotImplementedException();
        }

        public void ParseShowtimes()
        {
            var cities = new List<City>();
            using (var model = new MovieScheduleStatsEntities())
            {
                cities = model.Cities
                    .Include(x => x.Sources)
                    .Include(x => x.Satellites)
                    .Where(x => x.Sources.Any(xx => xx.TargetSite == TargetSiteShortcut &&
                        xx.MovieId == null && xx.CinemaId == null)).ToList();
                cities = cities.Where(x => x.Name == "Москва").ToList();
            }

            foreach (var city in cities)
            {
                ParseShowtimesForCityForDate(city, DateTime.Today);
            }
        }

        private void ParseShowtimesForCityForDate(City city, DateTime date)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var citySource =
                        city.Sources.First(
                            x => x.TargetSite == TargetSiteShortcut && x.MovieId == null && x.CinemaId == null);
                this.GetDefaultLogger().InfoFormat("{0}. Parsing {1}({2})", TargetSiteShortcut, city.Name, city.Id);
                var root = HttpSender.GetHtmlNodeResponse(citySource.URL, encoding: Encoding);
                var cinemaLinks = root.SelectNodes("//a[contains(@href,'multiplex')]");
                foreach (var cinemaNode in cinemaLinks)
                {
                    try
                    {
                        ParseShowtimesForCinemaForDay(city, cinemaNode, model, date);
                    }
                    catch (Exception ex)
                    {
                        this.GetDefaultLogger().Fatal(string.Format("{0}. Failed to parse {1}", TargetSiteShortcut, cinemaNode.InnerHtml), ex);
                    }
                    finally
                    {

                        //SleepManager.SleepRandomTimeout();
                    }
                }
            }
        }

        private void ParseShowtimesForCinemaForDay(City city, HtmlNode cinemaLinkNode, MovieScheduleStatsEntities model, DateTime date)
        {
            var cinemaLink = cinemaLinkNode.ParseLink(new Uri(Base));
            cinemaLink.Text = cinemaLink.Text.ReplaceBadChar().Replace("  ", " ");
            var originalCity = city.Name;
            var cinema = ParsingHelper.GetCinema(cinemaLink, city, TargetSiteShortcut, model, originalCity,
                GenerateSourceForCinema);

            var root = HttpSender.GetHtmlNodeResponse(cinemaLink.Reference.ToString(), encoding: Encoding);

            var showtimeNodes = root.SelectNodes("//div[@class='schedule_item']");

            foreach (var showtimeNode in showtimeNodes)
            {
                var movieLink = showtimeNode.ParseLink(new Uri(Base));
                movieLink.Text = movieLink.Text.ReplaceBadChar().Replace("  ", " ");

                var movie = model.Movies.FirstOrDefault(x => x.Title == movieLink.Text);
                if (movie == null) continue;
                List<Showtime> showtimes = new List<Showtime>();
                var sessionNode = showtimeNode.NextSibling.NextSibling;
                while (sessionNode.Name == "#text" ||
                        (sessionNode.Name == "div" && sessionNode.GetAttributeValue("class", "none") == "li"))
                {
                    var sessions = sessionNode.SelectNodes("span[@class='b']");
                    if (sessions != null)
                    {
                        var showtime = new Showtime
                            {
                                SessionsFormat = ParseSessionFormat(sessionNode).ToString(),
                                TargetSiteId = TargetSite.Id,
                                Movie = movie,
                                MovieId = movie.Id,
                                Cinema = cinema,
                                CinemaId = cinema.Id,
                                Date = date.Date,
                                CreationDate = DateTime.Now,
                                ParseRunId = ParseRunProvider.Instance.GetParseRun().Id,
                                SessionsCollection = sessions.Select(x => x.InnerText).ToList(),
                                CityId = city.Id
                            };
                        showtime = ParsingHelper.CheckShowtimePresent(showtime, model);
                        if (showtime != null)
                        {
                            showtimes.Add(showtime);
                        }
                    }

                    sessionNode = sessionNode.NextSibling;
                }
                foreach (var showtime in showtimes)
                {
                    model.Showtimes.Add(showtime);
                    model.SaveChanges();
                }
            }
        }

        private SessionFormat ParseSessionFormat(HtmlNode sessionNode)
        {
            var r = new Regex("[(]{1}([A-Z 0-9]{2,7})[):]{2}", RegexOptions.Compiled);
            var match = r.Match(sessionNode.TrimDecode());
            if (match.Success)
            {
                switch (match.Groups[1].Value)
                {
                    case "4DX 2D":
                        return SessionFormat.FourDX;
                        break;
                    case "IMAX 2D":
                        return SessionFormat.IMAX;
                        break;
                    case "3D":
                        return SessionFormat.ThreeD;
                        break;
                }
            }
            return SessionFormat.TwoD;
        }

        private Source GenerateSourceForCinema(Cinema cinema, City city, Link cinemaLink, string originalCity)
        {
            var source = new Source
        {
            Cinema = cinema,
            CityId = city.Id,
            TargetSite = TargetSiteShortcut,
            TargetSiteId = TargetSite.Id,
            Parameter = cinemaLink.Reference.ToString(),
            CreationDate = DateTime.Now,
            URL = new Uri(new Uri(URLs.Base), cinemaLink.Reference).ToString(),
            OriginalCity = originalCity,
            Text = cinemaLink.Text
        };
            return source;
        }

        public void ParseCities()
        {
            var root = HttpSender.GetHtmlNodeResponse(Base, encoding: Encoding);//GetHtmlResponse(Base, encoding: Encoding);
            var nodes = root.SelectNodes("//div[@class='li']/a");
            using (var model = new MovieScheduleStatsEntities())
            {
                foreach (var node in nodes)
                {
                    var link = node.ParseLink(new Uri(Base));
                    string linkText = link.Text == "Н.Новгород" ? "Нижний Новгород" : link.Text;
                    if (model.Sources.Any(x => x.TargetSiteId == TargetSite.Id && x.City != null && x.City.Name == linkText && x.Cinema == null && x.Movie == null)) continue;

                    string url = link.Reference.ToString();
                    var param = link.Reference.Segments[link.Reference.Segments.Length - 1].Trim(new[] { '/', ' ' });
                    var source = new Source
                    {
                        TargetSiteId = TargetSite.Id,
                        CityId = model.Cities.First(x => x.Name == linkText).Id,
                        TargetSite = TargetSiteShortcut,
                        URL = url,
                        Parameter = param,
                        Text = linkText,
                        CreationDate = DateTime.Now
                    };

                    model.Sources.Add(source);
                    model.SaveChanges();
                }
            }
        }

        public void Parse()
        {
            throw new NotImplementedException();
        }
    }
}
