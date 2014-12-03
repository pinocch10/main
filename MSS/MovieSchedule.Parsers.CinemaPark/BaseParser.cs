using System;
using System.Collections.Generic;
using System.Data.Common.EntitySql;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{
    public abstract class BaseParser : ILoggable
    {
        #region Fields

        private List<RawSession> _sessions = new List<RawSession>();

        #endregion

        #region Properties

        public abstract BaseParserSettings ParserSettings { get; }

        public List<Link> Cities { get; set; }

        public List<RawSession> Sessions
        {
            get { return _sessions; }
            set { _sessions = value; }
        }

        public Dictionary<Link, List<Link>> CityCinemas { get; set; }

        #endregion

        #region Public methods

        public void Parse()
        {
            ParseRawData();
            Sessions = CombineResult();
            SaveSessionsToDb(Sessions);
        }

        #endregion

        #region Private methods

        private void ParseRawData()
        {
            ParseCities();
            ParseCinemas();
            foreach (var date in ParserSettings.Dates)
            {
                ParseShowtimesForDate(date);
            }
        }

        private void ParseShowtimesForDate(DateTime date)
        {
            foreach (var pair in CityCinemas)
            {
                foreach (var cinema in pair.Value)
                {
                    ParseShowtimesForCinemaForDate(pair.Key, cinema, date);
                }
            }
        }

        private List<RawSession> CombineResult()
        {
            var sessions = Sessions.GroupBy(x => new
            {
                x.City,
                x.Cinema,
                x.Movie,
                x.Date,
                x.SessionFormat
            });
            var result = new List<RawSession>();
            foreach (var session in sessions)
            {
                var s = new RawSession
                {
                    City = session.Key.City,
                    Cinema = session.Key.Cinema,
                    Movie = session.Key.Movie,
                    Date = session.Key.Date,
                    SessionFormat = session.Key.SessionFormat,
                    Sessions = new List<string>()
                };
                var times = session.ToList().Select(x => x.Sessions).ToList();
                foreach (var t in times.SelectMany(time => time.Where(t => !s.Sessions.Contains(t))))
                {
                    s.Sessions.Add(t);
                }
                result.Add(s);
            }
            return result;
        }

        #endregion

        #region Vritual and abstract methods

        protected virtual void ParseCities(string overrideSearchUri = null)
        {
            Cities = new List<Link>();
            var root = HttpSender.GetHtmlNodeResponse(overrideSearchUri ?? ParserSettings.Base, encoding: ParserSettings.Encoding);
            var nodes = root.SelectNodes(ParserSettings.XPathCities);
            foreach (var node in nodes)
            {
                var baseUri = new Uri(overrideSearchUri ?? ParserSettings.Base);

                var link = node.ParseLink(baseUri);
                Cities.Add(link);
            }
        }

        protected virtual bool ParseCinemasForCity(Link city, out HtmlNode root)
        {
            if (!CityCinemas.ContainsKey(city))
                CityCinemas.Add(city, new List<Link>());

            root = HttpSender.GetHtmlNodeResponse(city.Reference.ToString(), encoding: ParserSettings.Encoding);
            var cinemaNodes = root.SelectNodes(ParserSettings.XPathCinemas);
            if (cinemaNodes == null) return false;
            foreach (var cinemaNode in cinemaNodes)
            {
                var cinemaLink = cinemaNode.ParseLink(ParserSettings.BaseUri);
                CityCinemas[city].Add(cinemaLink);
            }
            return true;
        }

        protected virtual void ParseCinemas()
        {
            CityCinemas = new Dictionary<Link, List<Link>>();
            foreach (var city in Cities)
            {
                HtmlNode root;
                ParseCinemasForCity(city, out root);
            }
        }

        protected abstract void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date);

        protected virtual Source GenerateSourceForCinema(Cinema cinema, City city, Link cinemaLink, string originalCity)
        {
            var source = new Source
            {
                Cinema = cinema,
                CityId = city.Id,
                TargetSite = ParserSettings.TargetSiteShortcut,
                TargetSiteId = ParserSettings.TargetSiteShortcut.GetTargetSite().Id,
                Parameter = cinemaLink.Reference.ToString(),
                CreationDate = DateTime.Now,
                URL = new Uri(ParserSettings.BaseUri, cinemaLink.Reference).ToString(),
                OriginalCity = originalCity,
                Text = cinemaLink.Text
            };
            return source;
        }

        protected virtual SessionFormat ParseSessionFormat(HtmlNode sessionNode)
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

        protected virtual void SaveSessionsToDb(List<RawSession> sessions)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                foreach (var session in sessions)
                {
                    SaveSessionToDb(session, model);
                    model.SaveChanges();
                }
            }
        }

        protected virtual void SaveSessionToDb(RawSession session, MovieScheduleStatsEntities model)
        {
            var movie = model.Movies.FirstOrDefault(x => x.Title == session.Movie.Text);
            var city = model.Cities.FirstOrDefault(x => x.Name == session.City.Text);
            //TODO Consider adding Levenshtein
            if (movie == null || city == null)
                return;
            var cinema = ParsingHelper.GetCinema(session.Cinema, city, ParserSettings.TargetSiteShortcut, model,
                null, GenerateSourceForCinema);

            var showtime = new Showtime
            {
                SessionsFormat = session.SessionFormat.ToString(),
                TargetSiteId = ParserSettings.TargetSiteShortcut.GetTargetSite().Id,
                Movie = movie,
                MovieId = movie.Id,
                Cinema = cinema,
                CinemaId = cinema.Id,
                Date = session.Date,
                CreationDate = DateTime.Now,
                ParseRunId = ParseRunProvider.Instance.GetParseRun().Id,
                CityId = city.Id,
                SessionsCollection = session.Sessions,
                Additional = string.Empty,
                URL = string.Empty
            };

            if (movie.Format == "2D" && session.SessionFormat == SessionFormat.ThreeD)
                showtime.SessionsFormat = SessionFormat.TwoD.ToString();
            showtime = ParsingHelper.CheckShowtimePresent(showtime, model);
            if (showtime != null)
            {
                model.Showtimes.Add(showtime);
            }
        }

        #endregion
    }
}