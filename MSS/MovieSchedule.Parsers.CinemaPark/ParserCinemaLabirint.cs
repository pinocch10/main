using System;
using System.Collections.Generic;
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
    public class ParserCinemaLabirintSettings : BaseParserSettings
    {
        private Uri _baseUri = null;

        public override string Base
        {
            get { return "http://labirint-cinema.ru/"; }
        }

        public override string TargetSiteShortcut
        {
            get { return "labirint-cinema.ru"; }
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

    public class ParserCinemaLabirint : BaseParser
    {
        private readonly ParserCinemaLabirintSettings _parserSettings = new ParserCinemaLabirintSettings();

        public override BaseParserSettings ParserSettings
        {
            get { return _parserSettings; }
        }

        protected override void ParseCities(string overrideSearchUri = null)
        {
            Cities = new List<Link>
            {
                new Link()
                {
                    Reference = new Uri("http://labirint-cinema.ru"),
                    Text = "Москва"
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
                        Reference = new Uri("http://labirint-cinema.ru/component/afisha/"),
                        Text = "Лабиринт (Жуковка)"
                    }
                });

            return true;
        }

        protected override void ParseShowtimesForCinemaForDate(Link city, Link cinema, DateTime date)
        {
            var root = HttpSender.GetHtmlNodeResponse(string.Format("{0}{1:yyyy-MM-dd}", cinema.Reference.ToString(), date), encoding: ParserSettings.Encoding);
            var movieNodes = root.SelectNodes("//div[@class='af_category_item']");
            if (movieNodes == null)
            {
                this.GetDefaultLogger().ErrorFormat("{0}. No movies found {1} {2}", ParserSettings.TargetSiteShortcut, city.Text, cinema.Text);
                return;
            }

            foreach (var movieNode in movieNodes)
            {
                HtmlNode movieTitleNode = movieNode.SelectSingleNode("div[@class='event_name_vote']/h2[@class='event_name']");
                var movieLink = movieTitleNode.ParseLink(new Uri(ParserSettings.Base));
                movieLink.Text = movieLink.Text.ReplaceBadChar().Replace("  ", " ");

                var sessionNodes = movieNode.SelectNodes("div[@class='event_ip_2']/div[@class='places_link_times']/div[@class='seanses_times']");

                var sessionFormat = SessionFormat.TwoD;
                var sessions = new List<string>();

                foreach (var sessionNode in sessionNodes)
                {
                    var timeNodes = sessionNode.SelectNodes("span");

                    foreach (var timeNode in timeNodes)
                    {
                        var nodeText = timeNode.TrimDecode();
                        if (!timeNode.Attributes["class"].Value.Contains("badge"))
                        {
                            var sf = SessionFormat.TwoD;
                            switch (nodeText)
                            {
                                case "2D сеансы:":
                                    sf = SessionFormat.TwoD;
                                    break;
                                case "3D сеансы:":
                                    sf = SessionFormat.ThreeD;
                                    break;
                            }
                            if (sessionFormat == sf) continue;
                            AddRawSession(city, cinema, date, movieLink, sessionFormat, sessions);
                            sessionFormat = sf;
                            sessions = new List<string>();
                        }
                        else
                        {
                            sessions.Add(NormalizeSessionTime(nodeText));
                        }
                    }
                }
                if (!sessions.Any()) continue;
                AddRawSession(city, cinema, date, movieLink, sessionFormat, sessions);
            }
        }

        private static string NormalizeSessionTime(string nodeText)
        {
            if (nodeText.Length == 5 && nodeText.Contains(":"))
                return nodeText;
            if (!nodeText.Contains(":"))
            {
                if (nodeText.Length == 3)
                    nodeText = "0" + nodeText;
                return nodeText.Insert(2, ":");
            }
            //Check more cases
            return nodeText;
        }

        private void AddRawSession(Link city, Link cinema, DateTime date, Link movieLink, SessionFormat sessionFormat,
            List<string> sessions)
        {
            var rs = new RawSession
            {
                City = city,
                Cinema = cinema,
                Movie = movieLink,
                SessionFormat = sessionFormat,
                Sessions = sessions.OrderBy(x => x, DataHelper.SessionsComparer).ToList(),
                Date = date
            };
            Sessions.Add(rs);
        }
    }
}