using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using HtmlAgilityPack;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Core.Managers;
using MovieSchedule.Data;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{
    public class ParserNewtorkLuxorSettings : BaseParserSettings
    {
        private Uri _baseUri = null;

        public override string Base
        {
            get { return "http://www.luxorfilm.ru/cinema/"; }
        }

        public override string TargetSiteShortcut
        {
            get { return "luxorfilm.ru"; }
        }

        public override string XPathCities
        {
            get { return "//div[@class='cinema_menu_item']/a"; }
        }

        public override string XPathCinemas
        {
            get { return "//div[@class='cin-link']/a"; }
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
                    //DateTime.Today.AddDays(1),
                    //DateTime.Today.AddDays(2),
                    //DateTime.Today.AddDays(3),
                };
                return result;
            }
        }
    }


    public class ParserNetworkLuxor : BaseParser
    {
        private readonly ParserNewtorkLuxorSettings _parserSettings = new ParserNewtorkLuxorSettings();

        public override BaseParserSettings ParserSettings
        {
            get { return _parserSettings; }
        }

        protected override void ParseCities(string overrideSearchUri = null)
        {
            base.ParseCities();
            Cities.Add(new Link { Text = "Москва", Reference = new Uri("http://www.luxorfilm.ru/cinema/") });
        }

        protected override bool ParseCinemasForCity(Link city, out HtmlNode root)
        {
            var result = base.ParseCinemasForCity(city, out root);
            if (result) return true;
            var cinemaLinkText = root.SelectSingleNode("//h1[@class='repertuar-header']/div/span/b").TrimDecode();
            CityCinemas[city].Add(new Link { Text = cinemaLinkText, Reference = city.Reference });
            return true;
        }

        protected override void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date)
        {
            var root = HttpSender.GetHtmlNodeResponse(cinema.Reference.ToString(), encoding: ParserSettings.Encoding);

            ParseShowtimesMain(city, cinema, date, root);

            var furtherDates =
                root.SelectSingleNode("//input[@name='ctl00$contentPlaceHolder$hdnEnableDates']").Attributes["value"].Value;

            var furtherDatesCollection = furtherDates.Split(';');
            var maxDate = furtherDatesCollection[furtherDatesCollection.Length - 1];
            for (int i = 1; i < furtherDatesCollection.Length; i++)
            {
                var furtherDate = furtherDatesCollection[i];
                var nextRoot = GetShowtimesForDate
                    (
                        furtherDate,
                        cinema,
                        root.SelectSingleNode("//input[@type='hidden' and @name='__VIEWSTATE' and @id='__VIEWSTATE']")
                            .Attributes["value"].Value,
                        root.SelectSingleNode(
                            "//input[@type='hidden' and @name='__EVENTVALIDATION' and @id='__EVENTVALIDATION']")
                            .Attributes["value"].Value,
                        furtherDates,
                        maxDate);
                ParseShowtimesMain(city, cinema,
                    DateTime.ParseExact(furtherDate, "dd.MM.yyyy", CultureInfo.InvariantCulture), nextRoot);
            }

            SleepManager.SleepRandomTimeout(500, 1000, 1);
        }

        private bool ParseShowtimesMain(Link city, Link cinema, DateTime date, HtmlNode root)
        {
            if (root == null)
            {
                this.GetDefaultLogger().InfoFormat("{0} {1}", city.Text, cinema.Text);
                return true;
            }
            var movieNodes = root.SelectNodes("//div[@class='cinema_container']");

            if (movieNodes == null)
            {
                this.GetDefaultLogger()
                    .ErrorFormat("{0}. No movies found {1} {2}", ParserSettings.TargetSiteShortcut, city.Text, cinema.Text);
                return true;
            }
            foreach (var movieNode in movieNodes)
            {
                var movie =
                    movieNode.SelectSingleNode("div[@class='cinema_time_info']/h3/a").ParseLink(new Uri(ParserSettings.Base));
                movie.Text = movie.Text.ReplaceBadChar().Replace("  ", " ");

                SessionFormat format = SessionFormat.TwoD;

                format = ParseSessionFormat(movie, format);

                var showtimes =
                    movieNode.SelectNodes(
                        "div[@class='cinema_time_info']/table/tr/td/div/div[@id='tabs-1001']/div/div[@class='d-right']/a");

                var rawSession = new RawSession
                {
                    Cinema = cinema,
                    City = city,
                    Movie = movie,
                    Date = date,
                    Sessions = showtimes.Select(x => x.FirstChild.TrimDecode()).ToList(),
                    SessionFormat = format
                };


                Sessions.Add(rawSession);
            }
            return false;
        }

        private HtmlNode GetShowtimesForDate(string furtherDate, Link cinema, string viewState, string eventValidation, string furtherDates, string maxDate)
        {
            Dictionary<string, string> postParams = new Dictionary<string, string>();
            AddEncodedParam(postParams, "ctl00$ctl08", "ctl00$contentPlaceHolder$UpdatePanel1|ctl00$contentPlaceHolder$drdDay");
            AddEncodedParam(postParams, "__EVENTTARGET", "ctl00$contentPlaceHolder$drdDay");
            AddEncodedParam(postParams, "__EVENTARGUMENT", "ctl00$contentPlaceHolder$drdDay");
            AddEncodedParam(postParams, "__LASTFOCUS", "ctl00$contentPlaceHolder$drdDay");

            AddEncodedParam(postParams, "__VIEWSTATE", viewState);
            AddEncodedParam(postParams, "__EVENTVALIDATION", eventValidation);

            AddEncodedParam(postParams, "ctl00$contentPlaceHolder$dateText", furtherDate);
            AddEncodedParam(postParams, "ctl00$contentPlaceHolder$hdnSelectedDate", furtherDate);
            AddEncodedParam(postParams, "ctl00$contentPlaceHolder$hdnEnableDates", furtherDates);
            AddEncodedParam(postParams, "ctl00$contentPlaceHolder$hdnMaxDate", maxDate);
            AddEncodedParam(postParams, "__ASYNCPOST", "true");

            var sb = new StringBuilder();
            foreach (var kvp in postParams)
            {
                sb.AppendFormat("{0}={1}&", kvp.Key, kvp.Value);
            }

            var root = HttpSender.GetHtmlNodeResponse(cinema.Reference.ToString(), encoding: ParserSettings.Encoding,
                parameters: sb.ToString());
            return root;
        }

        private static void AddEncodedParam(Dictionary<string, string> postParams, string key, string value)
        {
            postParams.Add(HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value));
        }

        private static SessionFormat ParseSessionFormat(Link movie, SessionFormat format)
        {
            if (ReplaceFormatInMovieTitle(movie, "Dolby Atmos 3D"))
            {
                return SessionFormat.TwoD;
            }
            else if (ReplaceFormatInMovieTitle(movie, "Dolby Atmos"))
            {
                return SessionFormat.TwoD;
            }
            else if (ReplaceFormatInMovieTitle(movie, "DBOX"))
            {
                return SessionFormat.DBOX;
            }
            else if (ReplaceFormatInMovieTitle(movie, "DBOX 3D"))
            {
                return SessionFormat.DBOX;
            }
            else if (ReplaceFormatInMovieTitle(movie, "IMAX"))
            {
                return SessionFormat.IMAX;
            }
            else if (ReplaceFormatInMovieTitle(movie, "IMAX 3D"))
            {
                return SessionFormat.IMAX;
            }
            else if (ReplaceFormatInMovieTitle(movie, "3D"))
            {
                return SessionFormat.ThreeD;
            }
            return format;
        }

        private static bool ReplaceFormatInMovieTitle(Link movie, string formatShortcut, bool detectOnly = false)
        {
            if (movie.Text.EndsWith(formatShortcut))
            {
                if (detectOnly) return true;
                movie.Text = movie.Text.Replace(formatShortcut, string.Empty).Trim();
                return true;
            }
            return false;
        }
    }
}