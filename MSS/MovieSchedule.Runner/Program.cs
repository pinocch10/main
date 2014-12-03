using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using MovieSchedule.Core;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Afisha;
using MovieSchedule.Parsers.CinemaPark;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Parsers.Kinometro;
using MovieSchedule.Parsers.Kinopoisk;

namespace MovieSchedule.Runner
{
    class Program : ILoggable
    {
        static void Main(string[] args)
        {
            var appender = new ColoredConsoleAppender
            {
                Threshold = Level.All,
                Layout = new PatternLayout(
                    "%level [%thread] %d{HH:mm:ss} - %message%newline"
                ),
            };
            appender.AddMapping(new ColoredConsoleAppender.LevelColors
            {
                Level = Level.Debug,
                ForeColor = ColoredConsoleAppender.Colors.Cyan
                    | ColoredConsoleAppender.Colors.HighIntensity
            });
            appender.AddMapping(new ColoredConsoleAppender.LevelColors
            {
                Level = Level.Info,
                ForeColor = ColoredConsoleAppender.Colors.Green
                    | ColoredConsoleAppender.Colors.HighIntensity
            });
            appender.AddMapping(new ColoredConsoleAppender.LevelColors
            {
                Level = Level.Warn,
                ForeColor = ColoredConsoleAppender.Colors.Purple
                    | ColoredConsoleAppender.Colors.HighIntensity
            });
            appender.AddMapping(new ColoredConsoleAppender.LevelColors
            {
                Level = Level.Error,
                ForeColor = ColoredConsoleAppender.Colors.Red
                    | ColoredConsoleAppender.Colors.HighIntensity
            });
            appender.AddMapping(new ColoredConsoleAppender.LevelColors
            {
                Level = Level.Fatal,
                ForeColor = ColoredConsoleAppender.Colors.White
                    | ColoredConsoleAppender.Colors.HighIntensity,
                BackColor = ColoredConsoleAppender.Colors.Red
            });

            appender.ActivateOptions();
            BasicConfigurator.Configure(appender);

            log4net.Config.XmlConfigurator.Configure();

            var logger = LogManager.GetLogger("DefaultLogger");
            logger.Info("Начало обработки");
            logger.Info("Initiazing ParseRun");
            ParseRunProvider.Instance.GetParseRun();
            logger.Info("ParseRun initiazed");

            var sw = new Stopwatch();
            sw.Start();
            logger.Info("Parsing kinometro started");
            var km = new KinometroParser();
            km.Parse();
            logger.InfoFormat("Parsing kinometro completed {0}", sw.Elapsed);

            var tasks = new List<Task>()
            {
                new Task(() => InitParser(new KinopoiskParser())),
                new Task(() => InitParser(new AfishaParser())),
                new Task(() => new ParserCinemaLabirint().Parse()),
                new Task(() => new ParserNewtorkCinemaPark().Parse()),
                new Task(() => new ParserCinemaTKino().Parse()),
                new Task(() => new ParserNetworkFormaulaKino().Parse()),
                new Task(() => new ParserNetworkLuxor().Parse())
            };

            logger.Info("Starting parallel run");

            sw.Restart();
            tasks.ForEach(x => x.Start());
            Task.WaitAll(tasks.ToArray());

            logger.InfoFormat("Completed parallel run: {0} {1}", tasks.Count, sw.Elapsed);

            logger.Info("Closing ParseRun");
            ParseRunProvider.Instance.CloseParseRun();
            logger.Info("ParseRun closed");
        }

        private static void InitParser(IMovieScheduleParser parser)
        {
            var logger = new Logger().GetDefaultLogger();
            logger.InfoFormat("{0} started", parser.GetType().Name);
            var sw = new Stopwatch();
            sw.Start();
            parser.Parse();
            sw.Stop();
            logger.InfoFormat("{0} completed {1}", parser.GetType().Name, sw.Elapsed);
        }
    }
}
