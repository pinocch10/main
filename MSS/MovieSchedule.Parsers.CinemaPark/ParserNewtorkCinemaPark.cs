using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{
    public class ParserNewtorkCinemaParkSettings : BaseParserSettings
    {
        private Uri _baseUri = null;

        public override string Base
        {
            get { return "http://m.cinemapark.ru/"; }
        }

        public override string TargetSiteShortcut
        {
            get { return "cinemapark.ru"; }
        }

        public override string XPathCities
        {
            get { return "//div[@class='li']/a"; }
        }

        public override string XPathCinemas
        {
            get { return "//a[contains(@href,'multiplex')]"; }
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

    public class ParserNewtorkCinemaPark : BaseParser
    {
        private readonly ParserNewtorkCinemaParkSettings _parserSettings = new ParserNewtorkCinemaParkSettings();

        public override BaseParserSettings ParserSettings
        {
            get { return _parserSettings; }
        }

        protected override void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date)
        {
            var root = HttpSender.GetHtmlNodeResponse(cinema.Reference.ToString(), encoding: ParserSettings.Encoding);
            var movieNodes = root.SelectNodes("//div[@class='schedule_item']");
            if (movieNodes == null)
            {
                this.GetDefaultLogger().ErrorFormat("{0}. No movies found {1} {2}", ParserSettings.TargetSiteShortcut, city.Text, cinema.Text);
                return;
            }

            foreach (var movieNode in movieNodes)
            {
                var movieLink = movieNode.ParseLink(new Uri(ParserSettings.Base));
                movieLink.Text = movieLink.Text.ReplaceBadChar().Replace("  ", " ");

                if (movieLink.Text.ToLower() == "ÃÎËÎÄÍÛÅ ÈÃÐÛ: ÑÎÉÊÀ-ÏÅÐÅÑÌÅØÍÈÖÀ. ×ÀÑÒÜ 1".ToLower())
                {
                    movieLink.Text = movieLink.Text.Replace("1", "I");
                }

                var sessionNode = movieNode.NextSibling.NextSibling;
                while (sessionNode.Name == "#text" ||
                       (sessionNode.Name == "div" &&
                        sessionNode.GetAttributeValue("class", "none") == "li"))
                {
                    var sessions = sessionNode.SelectNodes("span[@class='b']");
                    if (sessions != null)
                    {
                        var sp = new RawSession
                        {
                            City = city,
                            Cinema = cinema,
                            Movie = movieLink,
                            SessionFormat = ParseSessionFormat(sessionNode),
                            Sessions = sessions.Select(x => x.InnerText).ToList(),
                            Date = date
                        };
                        Sessions.Add(sp);
                    }

                    sessionNode = sessionNode.NextSibling;
                }
            }
        }
    }


}