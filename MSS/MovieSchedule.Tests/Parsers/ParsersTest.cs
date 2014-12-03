using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.SessionState;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Data.Entity;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Managers;
using MovieSchedule.Data;
using MovieSchedule.Parsers.Afisha;
using MovieSchedule.Parsers.CinemaPark;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Parsers.Kinometro;
using MovieSchedule.Parsers.Kinopoisk;

namespace MovieSchedule.Tests.Parsers
{
    [TestClass]
    public class ParsersTest
    {
        [TestMethod]
        public void SnapshotTests()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var showtime = new Showtime
                {
                    MovieId = 3028,
                    CinemaId = 643,
                    SessionsFormat = SessionFormat.ThreeD.ToString(),
                    TargetSiteId = 1,
                    Date = new DateTime(2014, 11, 11)
                };

                ParsingHelper.TryAddSnapshotSingle(model, showtime);
            }
        }

        [TestMethod]
        public void CinemaParkParserTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            var settings = new ParserNewtorkCinemaParkSettings();

            var parser = new ParserNewtorkCinemaPark();
            parser.Parse();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            var sessions = parser.Sessions.GroupBy(x => new
            {
                x.City,
                x.Cinema,
                x.Movie,
                x.SessionFormat
            });
            var sb = new StringBuilder();
            foreach (var session in sessions)
            {
                var times = session.ToList().Select(x => x.Sessions).ToList();
                var result = new List<string>();
                foreach (var t in times.SelectMany(time => time.Where(t => !result.Contains(t))))
                {
                    result.Add(t);
                }

                sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                    session.Key.City.Text,
                    session.Key.Cinema.Text,
                    session.Key.Movie.Text,
                    session.Key.SessionFormat,
                    string.Join("; ", result).Trim(new[] { ';', ' ' })));
            }

            //OrderBy(x => x.City.Text).ThenBy(x => x.Cinema.Text).ThenBy(x => x.Movie.Text).ToList();
            //var sb = new StringBuilder();
            //foreach (var session in sessions)
            //{
            //    sb.AppendLine(string.Format("{0} {1} {2} {3} {4}",
            //        session.City.Text,
            //        session.Cinema.Text,
            //        session.Movie.Text,
            //        session.SessionFormat,
            //        string.Join("; ", session.Sessions).Trim(new[] { ';', ' ' })));
            //}
            File.WriteAllText("C:/parse.txt", sb.ToString());
        }

        [TestMethod]
        public void KinomaxParserTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            var parser = new ParserNetworkKinomax();
            parser.Parse();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            LogParserRun(parser, "C:/kinomaxParser.txt");
        }

        [TestMethod]
        public void LuxorParserTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            var parser = new ParserNetworkLuxor();
            parser.Parse();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            LogParserRun(parser, "C:/luxorParser.txt");
        }

        [TestMethod]
        public void TKinoParserTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            var parser = new ParserCinemaTKino();
            parser.Parse();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            LogParserRun(parser, "C:/TKinoParser.txt");
        }

        [TestMethod]
        public void FormulaKinoParserTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            var parser = new ParserNetworkFormaulaKino();
            parser.Parse();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            LogParserRun(parser, "C:/FormulaKinoParser.txt");
        }

        private static void LogParserRun(BaseParser parser, string savePath)
        {
            var sessions = parser.Sessions.GroupBy(x => new
            {
                x.City,
                x.Cinema,
                x.Movie,
                x.SessionFormat
            });
            var sb = new StringBuilder();
            foreach (var session in sessions)
            {
                var times = session.ToList().Select(x => x.Sessions).ToList();
                var result = new List<string>();
                foreach (var t in times.SelectMany(time => time.Where(t => !result.Contains(t))))
                {
                    result.Add(t);
                }

                sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                    session.Key.City.Text,
                    session.Key.Cinema.Text,
                    session.Key.Movie.Text,
                    session.Key.SessionFormat,
                    string.Join("; ", result).Trim(new[] { ';', ' ' })));
            }

            //OrderBy(x => x.City.Text).ThenBy(x => x.Cinema.Text).ThenBy(x => x.Movie.Text).ToList();
            //var sb = new StringBuilder();
            //foreach (var session in sessions)
            //{
            //    sb.AppendLine(string.Format("{0} {1} {2} {3} {4}",
            //        session.City.Text,
            //        session.Cinema.Text,
            //        session.Movie.Text,
            //        session.SessionFormat,
            //        string.Join("; ", session.Sessions).Trim(new[] { ';', ' ' })));
            //}
            File.WriteAllText(savePath, sb.ToString());
        }

        [TestMethod]
        public void LabirintParserTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            var settings = new ParserCinemaLabirintSettings();

            var parser = new ParserCinemaLabirint();
            parser.Parse();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            var sessions = parser.Sessions.GroupBy(x => new
            {
                x.City,
                x.Cinema,
                x.Movie,
                x.SessionFormat
            });
            var sb = new StringBuilder();
            foreach (var session in sessions)
            {
                var times = session.ToList().Select(x => x.Sessions).ToList();
                var result = new List<string>();
                foreach (var t in times.SelectMany(time => time.Where(t => !result.Contains(t))))
                {
                    result.Add(t);
                }

                sb.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                    session.Key.City.Text,
                    session.Key.Cinema.Text,
                    session.Key.Movie.Text,
                    session.Key.SessionFormat,
                    string.Join("; ", result).Trim(new[] { ';', ' ' })));
            }

            //OrderBy(x => x.City.Text).ThenBy(x => x.Cinema.Text).ThenBy(x => x.Movie.Text).ToList();
            //var sb = new StringBuilder();
            //foreach (var session in sessions)
            //{
            //    sb.AppendLine(string.Format("{0} {1} {2} {3} {4}",
            //        session.City.Text,
            //        session.Cinema.Text,
            //        session.Movie.Text,
            //        session.SessionFormat,
            //        string.Join("; ", session.Sessions).Trim(new[] { ';', ' ' })));
            //}
            File.WriteAllText("C:/parse.txt", sb.ToString());
        }

        [TestMethod]
        public void KinometroParse()
        {
            var parser = new KinometroParser();

            parser.ParseDistributors();
            SleepManager.SleepRandomTimeout(3, 5);
            parser.ParseAnalytics();
            SleepManager.SleepRandomTimeout(3, 5);
            parser.ParseReleases();
            SleepManager.SleepRandomTimeout(3, 5);
            parser.ParseReleases3D();
            parser.SuspendReleases();
        }

        [TestMethod]
        public void KinometroParseAnalytics()
        {
            var parser = new KinometroParser();
            parser.Parse();
        }

        [TestMethod]
        public void KinopoiskMoviesParse()
        {
            var parser = new KinopoiskParser();
            parser.ParseMovies(3);
        }

        [TestMethod]
        public void CinemaparkParse()
        {
            var parser = new CinemaParkParser();
            parser.ParseShowtimes();

            LogShowTimes();
        }

        [TestMethod]
        public void AAKinopoiskCityParse()
        {
            var parser = new KinopoiskParser();
            var cinemas = parser.ParseCinemas();

            List<Cinema> storedCinemas;
            using (var model = new MovieScheduleStatsEntities())
            {
                storedCinemas = model.Cinemas.Include(x => x.Sources).Include(x => x.City).ToList();
            }
            Dictionary<string, List<Link>> fullMatch = new Dictionary<string, List<Link>>();
            foreach (var pair in cinemas)
            {
                foreach (var cinema in pair.Value)
                {
                    if (storedCinemas.Any(x => x.Name == cinema.Text && x.City.Name == pair.Key))
                    {
                        fullMatch.AddOrUpdate(pair.Key, new List<Link> { cinema });
                    }
                    if (storedCinemas.Any(x => x.Name == cinema.Text && x.City.Name != pair.Key))
                    {
                        fullMatch.AddOrUpdate(pair.Key, new List<Link> { cinema });
                    }
                }
            }

            LogShowTimes();
        }

        [TestMethod]
        public void ParseCities()
        {
            new KinopoiskParser().ParseCities();
            //new AfishaParser().ParseCities();
        }

        [TestMethod]
        public void Snapshots()
        {
            var sw = new Stopwatch();
            sw.Start();
            ParsingHelper.TryAddSnapshots();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
        }

        [TestMethod]
        public void KinopoiskParse()
        {
            var parser = new KinopoiskParser();
            //parser.Parse();
            //parser.ParseCities();
            parser.ParseShowtimes();

            LogShowTimes();
        }

        [TestMethod]
        public void AfishaParse()
        {
            var parser = new AfishaParser();
            parser.Parse();

            LogShowTimes();
        }

        [TestMethod]
        public void KinopoiskFillMovieDetails()
        {
            var parser = new KinopoiskParser();
            parser.FillMoviesDetails();

        }

        private static void LogShowTimes()
        {
            Console.WriteLine("Found {0} distinct session counts, overall number of sessions: {1}", ParsingHelper.SessionTimes.Count, ParsingHelper.SessionTimes.Values.Sum());
            //foreach (var st in ParsingHelper.SessionTimes)
            //{
            //    Console.WriteLine("{0} -> {1}", st.Key.PadRight(5), st.Value.ToString(CultureInfo.InvariantCulture).PadLeft(4));
            //}
        }
    }
}
