using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Core.Managers;
using MovieSchedule.Data;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{

    public class ParserNewtorkFormulaKinoSettings : BaseParserSettings
    {
        private Uri _baseUri = null;

        public override string Base
        {
            get { return "http://www.formulakino.ru/"; }
        }

        public override string TargetSiteShortcut
        {
            get { return "formulakino.ru"; }
        }

        public override string XPathCities
        {
            get { return "//nav[@class='top_submenu']/a"; }
        }

        public override string XPathCinemas
        {
            get { return "//div[@class='cinema-item']/h3/a"; }
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

    public class ParserNetworkFormaulaKino : BaseParser
    {
        private readonly ParserNewtorkFormulaKinoSettings _parserSettings = new ParserNewtorkFormulaKinoSettings();

        public override BaseParserSettings ParserSettings
        {
            get { return _parserSettings; }
        }

        protected override void ParseCities(string overrideSearchUri = null)
        {
            base.ParseCities("http://www.formulakino.ru/cinemas");
        }

        protected override void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date)
        {
            var root = HttpSender.GetHtmlNodeResponse(cinema.Reference.ToString(), encoding: ParserSettings.Encoding);
            if (root == null)
            {
                this.GetDefaultLogger().InfoFormat("{0} {1}", city.Text, cinema.Text);
                return;
            }
            var movieNodes = root.SelectNodes("//div[@class='cinema-movie-schedule']/section[@class='schedule']/div[@class='cinemas']/div[@class='item']");

            if (movieNodes == null)
            {
                this.GetDefaultLogger().ErrorFormat("{0}. No movies found {1} {2}", ParserSettings.TargetSiteShortcut, city.Text, cinema.Text);
                return;
            }
            foreach (var movieNode in movieNodes)
            {
                var movie = movieNode.SelectSingleNode("b").ParseLink(new Uri(ParserSettings.Base));
                movie.Text = movie.Text.ReplaceBadChar().Replace("  ", " ");

                var dateNodes = movieNode.SelectNodes("div[@class='hall clearfix']/div[@class='times']/span[contains(@class,'date')]");

                foreach (var dateNode in dateNodes)
                {
                    var dateRaw = dateNode.Attributes["data-date"].Value;

                    var showtimeDate = DateTime.ParseExact(dateRaw, "dd.MM.yyyy", CultureInfo.InvariantCulture);

                    var showtime = new RawSession
                    {
                        Cinema = cinema,
                        City = city,
                        Movie = movie,
                        Date = showtimeDate
                    };

                    var timeNodes = dateNode.SelectNodes("span/a[contains(@class,'tr_chart')]");
                    foreach (var timeNode in timeNodes)
                    {
                        var sessionTime = timeNode.FirstChild.TrimDecode();
                        var formatString = timeNode.NextSibling.NextSibling.TrimDecode();
                        SessionFormat format = SessionFormat.TwoD;
                        switch (formatString)
                        {
                            case "2D":
                                format = SessionFormat.TwoD;
                                break;
                            case "3D":
                                format = SessionFormat.ThreeD;
                                break;
                        }

                        if (format == showtime.SessionFormat || !showtime.Sessions.Any())
                        {
                            showtime.SessionFormat = format;
                            if (!showtime.Sessions.Contains(sessionTime))
                                showtime.Sessions.Add(sessionTime);
                        }
                        else
                        {
                            Sessions.Add(showtime);
                            showtime = showtime.Clone();
                            showtime.SessionFormat = format;
                            showtime.Sessions = new List<string> { sessionTime };
                        }
                    }
                    Sessions.Add(showtime);
                }
            }
            SleepManager.SleepRandomTimeout(500, 1000, 1);
        }
    }
}