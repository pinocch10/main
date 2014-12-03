using System;
using System.Linq;
using MovieSchedule.Core.Logging;

namespace MovieSchedule.Data
{
    public sealed class ParseRunProvider : ILoggable
    {
        private ParseRun _parseRun = null;
        private readonly object _locker = new object();

        private static readonly ParseRunProvider instance = new ParseRunProvider();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static ParseRunProvider()
        {
        }

        private ParseRunProvider()
        {
        }

        public static ParseRunProvider Instance
        {
            get
            {
                return instance;
            }
        }

        public ParseRun GetParseRun(MovieScheduleStatsEntities model = null)
        {
            try
            {
                if (_parseRun != null) return _parseRun;
                lock (_locker)
                {
                    if (model == null)
                    {
                        using (model = new MovieScheduleStatsEntities())
                        {
                            GenerateParseRun(model);
                        }
                    }
                    else
                    {
                        GenerateParseRun(model);
                    }
                }
                return _parseRun;

            }
            catch (Exception ex)
            {
                this.GetDefaultLogger().Fatal("Failed to get ParseRun", ex);
            }
            return null;
        }

        public ParseRun CloseParseRun()
        {
            try
            {
                if (_parseRun == null) return null;
                using (var model = new MovieScheduleStatsEntities())
                {
                    _parseRun = model.ParseRuns.FirstOrDefault(x => x.Id == _parseRun.Id);
                    if (_parseRun == null) return null;
                    _parseRun.Completed = DateTime.Now;
                    model.SaveChanges();
                    _parseRun = null;
                    return _parseRun;
                }

            }
            catch (Exception ex)
            {
                this.GetDefaultLogger().Fatal("Failed to close ParseRun", ex);
            }
            return null;
        }

        public void IncrementNewShowtimesCount(int targetSiteId)
        {
            IncrementShowtimeCount(targetSiteId, false);
        }

        public void IncrementUpdatedShowtimesCount(int targetSiteId)
        {
            IncrementShowtimeCount(targetSiteId, true);
        }

        private void IncrementShowtimeCount(int targetSiteId, bool wasUpdated)
        {
            try
            {
                using (var model = new MovieScheduleStatsEntities())
                {
                    if (_parseRun == null)
                        _parseRun = GetParseRun(model);
                    lock (_locker)
                    {
                        var pri = model.ParseRunInfoes.FirstOrDefault(x => x.TargetSiteId == targetSiteId &&
                                                                           x.ParseRunId == _parseRun.Id);

                        if (pri == null)
                        {
                            pri = new ParseRunInfo
                            {
                                ParseRunId = _parseRun.Id,
                                TargetSiteId = targetSiteId
                            };
                            model.ParseRunInfoes.Add(pri);
                            model.SaveChanges();
                        }

                        if (wasUpdated) pri.ShowtimesCountUpdated++;
                        else pri.ShowtimesCountNew++;

                        _parseRun = model.ParseRuns.FirstOrDefault(x => x.Id == _parseRun.Id);

                        if (_parseRun != null)
                        {
                            _parseRun.ShowtimesCount++;
                        }

                        model.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                this.GetDefaultLogger().Fatal("Failed to close increment showtime count", ex);
            }
        }

        private void GenerateParseRun(MovieScheduleStatsEntities model)
        {
            _parseRun = new ParseRun
            {
                Started = DateTime.Now
            };
            model.ParseRuns.Add(_parseRun);
            model.SaveChanges();
            this.GetLogger("ParseRunLogger").InfoFormat("New parse run generated Id: {0}", _parseRun.Id);
        }
    }
}