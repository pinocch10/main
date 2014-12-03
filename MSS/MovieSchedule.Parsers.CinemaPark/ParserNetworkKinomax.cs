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
    public class ParserNetworkKinomaxSettings : BaseParserSettings
    {
        private Uri _baseUri = null;

        public override string Base
        {
            get { return "http://kinomax.ru/"; }
        }

        public override string TargetSiteShortcut
        {
            get { return "kinomax.ru"; }
        }

        public override string XPathCities
        {
            get { return "//div[@class='city-column']/a"; }
        }

        public override string XPathCinemas
        {
            get { return "//section[@class='spaced finder']/article/b/a"; }
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
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

    public class ParserNetworkKinomax : BaseParser
    {
        #region Settings

        private readonly ParserNetworkKinomaxSettings _parserSettings = new ParserNetworkKinomaxSettings();

        public override BaseParserSettings ParserSettings
        {
            get { return _parserSettings; }
        }

        #endregion

        protected override void ParseCities(string overrideSearchUri = null)
        {
            base.ParseCities("http://kinomax.ru/home/cities");
            foreach (var city in Cities)
            {
                if (!city.Reference.ToString().Contains("finder"))
                    city.Reference = new Uri(city.Reference.ToString().Replace("/?", "/finder/?"));
            }
        }

        protected override bool ParseCinemasForCity(Link city, out HtmlNode root)
        {
            //if (city.Text != "Москва") return;
            if (!CityCinemas.ContainsKey(city))
                CityCinemas.Add(city, new List<Link>());

            root = HttpSender.GetHtmlNodeResponse(city.Reference.ToString(), encoding: ParserSettings.Encoding);
            var cinemaNodes = root.SelectNodes(ParserSettings.XPathCinemas);
            if (cinemaNodes != null)
            {
                foreach (var cinemaLink in
                    cinemaNodes.Select(cinemaNode => cinemaNode.ParseLink(ParserSettings.BaseUri)))
                {
                    //if (!cinemaLink.Text.Contains("Солярис")) continue;

                    cinemaLink.Text = cinemaLink.Text.Replace(city.Text, string.Empty).Trim();
                    CityCinemas[city].Add(cinemaLink);
                }
            }
            else
            {
                var cinemaLink = city.Clone();
                cinemaLink.Text = string.Format("Киномакс-{0}", city.Text);
                CityCinemas[city].Add(cinemaLink);
            }

            return true;
        }

        protected override void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date)
        {
            var root = HttpSender.GetHtmlNodeResponse(cinema.Reference.ToString(), encoding: ParserSettings.Encoding);
            var movieNodes = root.SelectNodes("//div[@class='filmdesc clearboth']");
            if (movieNodes == null)
            {
                this.GetDefaultLogger()
                    .ErrorFormat("{0}. No movies found {1} {2}", ParserSettings.TargetSiteShortcut, city.Text,
                        cinema.Text);
                return;
            }

            foreach (var movieNode in movieNodes)
            {
                HtmlNode movieTitleNode =
                    movieNode.SelectSingleNode(
                        "div[@class='filmpic clearboth']/div[@class='rightfilm']/div[@class='filmtitle']/h1/a");
                var movieLink = movieTitleNode.ParseLink(new Uri(ParserSettings.Base));
                movieLink.Text = movieLink.Text.ReplaceBadChar().Replace("  ", " ");

                //var showtimeNodes = movieNode.SelectNodes("div[@class='showtime']/table[@class='stdtbl']");
                var showtimeNodes = movieNode.SelectNodes("div[@class='showtime']/table");
                foreach (var showtimeNode in showtimeNodes)
                {
                    var format = showtimeNode.SelectSingleNode("tbody/tr/td[@rowspan]").TrimDecode();
                    SessionFormat sessionFormat = ParseSessionFormat(format);

                    var sessionNodes = showtimeNode.SelectNodes("tbody/tr");

                    foreach (var sessionNode in sessionNodes)
                    {
                        var timeNodes = sessionNode.SelectNodes("td[not(@rowspan)]");
                        if (timeNodes == null) continue;
                        DateTime sessionDate;
                        string rawDate = timeNodes[0].TrimDecode();
                        rawDate = string.Format("{0}.{1}", rawDate.Substring(4), DateTime.Today.Year);
                        if (!DateTime.TryParseExact(rawDate, "dd.MM.yyyy", CultureInfo.CurrentCulture, DateTimeStyles.None, out sessionDate))
                        {
                            sessionDate = date;
                        }
                        var sessions = timeNodes[1].SelectNodes("span").Select(x => x.TrimDecode());
                        var rs = new RawSession
                        {
                            City = city,
                            Cinema = cinema,
                            Movie = movieLink,
                            SessionFormat = sessionFormat,
                            Sessions = sessions.OrderBy(x => x, DataHelper.SessionsComparer).ToList(),
                            Date = sessionDate
                        };
                        Sessions.Add(rs);
                    }
                }
            }
        }

        private static SessionFormat ParseSessionFormat(string format)
        {
            SessionFormat sessionFormat = SessionFormat.TwoD;
            switch (format)
            {
                case "Формат 3D":
                    sessionFormat = SessionFormat.ThreeD;
                    break;
                case "Формат 2D":
                    sessionFormat = SessionFormat.TwoD;
                    break;
                case "Формат IMAX 3D":
                case "Формат IMAX 2D":
                    sessionFormat = SessionFormat.IMAX;
                    break;
            }
            return sessionFormat;
        }
    }
}