using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{
    public class ParserCinemaTKinoSettings : BaseParserSettings
    {
        private Uri _baseUri = null;

        public override string Base
        {
            get { return "http://www.tkino-mozdok.ru"; }
        }

        public override string TargetSiteShortcut
        {
            get { return "tkino-mozdok.ru"; }
        }

        public override string XPathCities
        {
            get { return null; }
        }

        public override string XPathCinemas
        {
            get { return null; }
        }

        public override Encoding Encoding
        {
            get { return Encoding.GetEncoding(1251); }
        }

        public override Uri BaseUri
        {
            get { return _baseUri ?? (_baseUri = new Uri(Base)); }
        }

        public override List<DateTime> Dates
        {
            get
            {
                var result = new List<DateTime>
                {
                    DateTime.Today,
                    DateTime.Today.AddDays(1),
                    DateTime.Today.AddDays(2),
                    DateTime.Today.AddDays(3),
                };
                return result;
            }
        }
    }

    public class ParserCinemaTKino : BaseParser
    {
        private readonly ParserCinemaTKinoSettings _parserSettings = new ParserCinemaTKinoSettings();

        public override BaseParserSettings ParserSettings
        {
            get { return _parserSettings; }
        }

        protected override void ParseCities(string overrideSearchUri = null)
        {
            Cities = new List<Link>
            {
                new Link
                {
                    Reference = new Uri("http://www.tkino-mozdok.ru"),
                    Text = "Моздок"
                }
            };
        }

        protected override bool ParseCinemasForCity(Link city, out HtmlNode root)
        {
            root = null;
            if (!CityCinemas.ContainsKey(city))
                CityCinemas.Add(city, new List<Link>
                {
                    new Link
                    {
                        Reference = new Uri("http://www.tkino-mozdok.ru/today.html"),
                        Text = "Территория Кино"
                    }
                });

            return true;
        }

        protected override void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date)
        {
            var root = HttpSender.GetHtmlNodeResponse(cinema.Reference.ToString(), encoding: ParserSettings.Encoding);
            var movieTable = root.SelectSingleNode("//div[@id='raspisamie_seansov']/div/table");
            if (movieTable == null)
            {
                this.GetDefaultLogger().ErrorFormat("{0}. No movies found {1} {2}", ParserSettings.TargetSiteShortcut, city.Text, cinema.Text);
                return;
            }

            List<RawSession> localSessions = new List<RawSession>();

            var movieNodes = movieTable.SelectNodes("//tr[@align='center']");

            foreach (var movieNode in movieNodes)
            {
                var cells = movieNode.SelectNodes("td[@class]");
                if (cells == null) continue;

                var showtime = new RawSession
                {
                    City = city,
                    Cinema = cinema,
                    Date = date
                };
                foreach (var cell in cells)
                {
                    var cellValue = cell.FirstChild.TrimDecode();
                    var cellClass = cell.Attributes["class"].Value;
                    switch (cellClass)
                    {
                        case "text1":
                            var formatText = cellValue.Substring(cellValue.Length - 2);
                            switch (formatText)
                            {
                                case "2D":
                                    showtime.SessionFormat = SessionFormat.TwoD;
                                    break;
                                case "3D":
                                    showtime.SessionFormat = SessionFormat.ThreeD;
                                    break;
                            }
                            showtime.Movie = new Link { Text = cellValue.Replace(formatText, string.Empty).Trim() };
                            break;
                        case "text4":
                            showtime.Sessions.Add(cellValue);
                            break;
                    }
                }
                try
                {
                    if (showtime.Movie == null) continue;
                    var parsedSession =
                    localSessions.FirstOrDefault(
                        x => x.Movie.Text == showtime.Movie.Text && x.SessionFormat == showtime.SessionFormat);
                    if (parsedSession == null)
                    {
                        localSessions.Add(showtime);
                    }
                    else
                    {
                        foreach (var session in showtime.Sessions.Where(session => !parsedSession.Sessions.Contains(session)))
                        {
                            parsedSession.Sessions.Add(session);
                        }
                    }
                }
                catch (Exception)
                {

                }

            }

            var dateRangeNode = root.SelectSingleNode("//span[@class='fine']");
            if (dateRangeNode != null)
            {
                var text = dateRangeNode.TrimDecode();
                var dates = text.Split('-');
                DateTime leftDate = DateTime.ParseExact(dates[0].Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture);
                DateTime rightDate = DateTime.ParseExact(dates[1].Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture);
                while (leftDate <= rightDate)
                {
                    foreach (var localSession in localSessions)
                    {
                        localSession.Date = leftDate;
                        Sessions.Add(localSession.Clone());
                    }
                    leftDate = leftDate.AddDays(1);
                }
            }
        }
    }
}