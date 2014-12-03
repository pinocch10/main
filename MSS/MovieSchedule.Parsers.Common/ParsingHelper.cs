using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MovieSchedule.Core;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;

namespace MovieSchedule.Parsers.Common
{
    public static class ParsingHelper
    {
        public static Cinema GetCinema(Link cinemaLink, City city, string targetSite, MovieScheduleStatsEntities model, string originalCity, SourceForCinema method)
        {
            var originalText = cinemaLink.Text;
            cinemaLink.Text = cinemaLink.Text.RemoveValueInBrackets(city.Name);
            //var possibleCityName = GetCityNameFromLink(cinemaLink);
            //var cinemaNameWithoutCity = cinemaLink.Text.Replace(string.Format("({0})", possibleCityName), string.Empty).Trim();
            //string[] cinemaNames = new[] { cinemaNameWithoutCity, cinemaLink.Text };
            var cinema = model.Cinemas.FirstOrDefault(x => x.CityId == city.Id && x.Name == cinemaLink.Text) ??
                          model.Cinemas.FirstOrDefault(x => x.CityId == city.Id &&
                              x.Sources.Any(xx => xx.TargetSite == targetSite &&
                              (xx.Text == cinemaLink.Text || xx.Text == cinemaLink.Text)));


            #region Get missing cinema

            if (cinema == null && originalCity == city.Name)
            {
                city = model.Cities.Include("Satellites").FirstOrDefault(x => x.Id == city.Id);

                foreach (var satellite in city.Satellites)
                {
                    cinema = model.Cinemas.FirstOrDefault(x => x.CityId == satellite.Id
                                 && cinemaLink.Text == x.Name) ??
                             model.Cinemas.FirstOrDefault(x => x.CityId == satellite.Id
                                 && x.Sources.Any(xx => xx.TargetSite == targetSite && xx.Text == cinemaLink.Text)) ??
                             model.Cinemas.FirstOrDefault(x => x.Sources.Any(xx => xx.TargetSite == targetSite
                                 && xx.Text == cinemaLink.Text && x.OriginalCity == satellite.Name));
                    if (cinema != null) break;
                }
            }

            if (cinema == null)
            {
                cinema = model.Cinemas.FirstOrDefault(x => x.City.Name == originalCity && cinemaLink.Text == x.Name);
                if (cinema == null)
                {
                    cinema = model.Cinemas.FirstOrDefault(x => x.City.Name == originalCity &&
                       x.Sources.Any(xx => xx.TargetSite == targetSite && xx.Text == cinemaLink.Text)) ??
                       model.Cinemas.FirstOrDefault(x => x.Sources.Any(xx => xx.TargetSite == targetSite && xx.Text == cinemaLink.Text && x.OriginalCity == originalCity));
                }
            }

            #endregion

            #region Get completely missing cinemas
            //Removed as obsolette
            //if (cinema == null)
            //{
            //    new Logger().GetLogger("MissingCinemaLogger").InfoFormat("{0} -> {1} ({2} / {3})", originalText, cinemaLink.Text, city.Name, originalCity);

            //    cinema = model.Cinemas.FirstOrDefault(x => (x.City.Name == city.Name || true) && cinemaLink.Text == x.Name);
            //    if (cinema == null)
            //    {
            //        new Logger().GetLogger("MissingCinemaLogger").Info("First approximation failed");
            //        cinema = model.Cinemas.FirstOrDefault(x => (x.City.Name == city.Name || true) &&
            //           x.Sources.Any(xx => xx.TargetSite == targetSite && xx.Text == cinemaLink.Text));
            //        if (cinema == null)
            //            new Logger().GetLogger("MissingCinemaLogger").Info("Second approximation failed. Non-master list cinema detected");
            //    }
            //    if (cinema != null)
            //    {
            //        new Logger().GetLogger("MissingCinemaLogger").InfoFormat("Substitution found {0} ({1})", cinema.Name, cinema.City.Name);
            //    }
            //}

            #endregion

            if (cinema != null)
            {
                var source = cinema.Sources.FirstOrDefault(x =>
                                                           x.TargetSite == targetSite &&
                                                           (x.Text == cinemaLink.Text || x.Text == cinemaLink.Text));
                if (source != null)
                {
                    source.Parameter = cinemaLink.Reference.ToString();
                    source.URL = cinemaLink.Reference.ToString();
                    source.OriginalCity = originalCity;
                }
                else
                {
                    source = method.Invoke(cinema, city, cinemaLink, originalCity);

                    model.Sources.Add(source);
                }
            }
            else
            {

                cinema = new Cinema
                     {
                         Name = cinemaLink.Text,
                         CityId = city.Id,
                         OriginalCity = originalCity,
                         CreationDate = DateTime.Now
                     };
                var source = method.Invoke(cinema, city, cinemaLink, originalCity);

                model.Cinemas.Add(cinema);
                model.Sources.Add(source);
            }
            return cinema;
        }

        public static Country GetCountry(MovieScheduleStatsEntities model)
        {
            var country = model.Countries.FirstOrDefault();
            if (country == null)
            {
                country = new Country
                    {
                        Name = "Российская Федерация",
                        Code = "RU"
                    };
                model.Countries.Add(country);
                model.SaveChanges();
            }
            return country;
        }

        public static City TryGetReplacementCity(this IMovieScheduleParser parser, Link cinemaLink, MovieScheduleStatsEntities model, Uri cityUri)
        {
            var cinemaSegment = cinemaLink.Reference.Segments[1];
            var citySegment = cityUri.Segments[1];
            City result = null;
            if (cinemaSegment != citySegment)
            {
                var targetSite = parser.GetTargetSite();
                result = model.Cities.FirstOrDefault(
                     x => x.Sources.Any(xx => xx.TargetSite == targetSite && xx.Parameter == cinemaSegment));
            }

            return result;
        }

        public static string RemoveCityNameFromCinemaName(Link cinemaLink, MovieScheduleStatsEntities model)
        {
            var regex = new Regex(@"[/(]([\sа-яА-Я/-]*)[/)]");
            var match = regex.Match(cinemaLink.Text);
            if (match.Success)
            {
                var cityName = match.Groups[1].Value;
                if (model.Cities.Any(x => x.Name == cityName))
                {
                    return cinemaLink.Text.Replace(match.Groups[0].Value, String.Empty).Trim();
                }
            }
            return cinemaLink.Text;
        }

        public static City TryGetReplacementCity(Link cinemaLink, MovieScheduleStatsEntities model)
        {
            var replacementCityName = cinemaLink.Text.GetValueInBrackets();
            City replacementCity = null;
            if (!String.IsNullOrWhiteSpace(replacementCityName))
            {
                replacementCity = model.Cities.Include("Satellites").FirstOrDefault(x => x.Name == replacementCityName);
                //if (replacementCity != null)
                //{
                //    cinemaLink.Text = cinemaLink.Text.Replace(match.Groups[0].Value, String.Empty).Trim();
                //}
            }
            return replacementCity;
        }

        public static Cinema GetCinemaForShowtime(MovieScheduleStatsEntities model, City cityToUse, Link cinemaLink, string originalCity, SourceForCinema method)
        {
            cinemaLink.Text = cinemaLink.Text.RemoveValueInBrackets(cityToUse.Name);

            Cinema cinema = model.Cinemas.FirstOrDefault(x =>
                                           x.CityId == cityToUse.Id &&
                                           (x.Name == cinemaLink.Text ||
                                            x.Sources.Any(xx => xx.Parameter == cinemaLink.Reference.AbsoluteUri) ||
                                            x.Sources.Any(xx => xx.Text == cinemaLink.Text)));

            if (cinema == null)
            {
                cinema = new Cinema
                    {
                        Name = cinemaLink.Text,
                        CityId = cityToUse.Id,
                        CreationDate = DateTime.Now
                    };
                model.Cinemas.Add(cinema);
            }
            var source = method.Invoke(cinema, cityToUse, cinemaLink, originalCity);
            model.Sources.Add(source);
            //else if (cinema.Sources.All(x => x.Parameter != cinemaLink.Reference.ToString()))
            //{
            //    var source = method.Invoke(cinema, cityToUse, cinemaLink);
            //    model.Sources.Add(source);
            //}
            return cinema;
        }

        public static Dictionary<string, int> SessionTimes = new Dictionary<string, int>();

        public static void PopulateSessions(Showtime showtime, HtmlNodeCollection timeNodes)
        {
            var sb = new StringBuilder();
            showtime.SessionsCount = 0;
            var sessions = new List<string>();
            foreach (var cn in timeNodes.Where(x =>
                x.Attributes == null ||
                x.Attributes["class"] == null ||
                x.Attributes["class"].Value != "title"))
            {
                var sessionTime = NodeHelper.TrimDecode((HtmlNode)cn);
                if (!sessions.Contains(sessionTime))
                    sessions.Add(sessionTime);

                if (SessionTimes.ContainsKey(sessionTime))
                    SessionTimes[sessionTime]++;
                else
                    SessionTimes.Add(sessionTime, 1);
                showtime.SessionsCount++;
                sb.AppendFormat("{0}{1} ", sessionTime.NormalizeSessionTime(), Constants.SessionDelimiter);
            }

            showtime.SessionsRaw = sb.ToString().Trim(' ', Constants.SessionDelimiter);
            showtime.SessionsCollection = sessions;

            //showtime.SessionsCollection = showtime.SessionsCollection.OrderBy(x => x).ToList();
        }

        public static string NormalizeSessionTime(this string sessionTime)
        {
            if (sessionTime.Length == 5) return sessionTime;
            if (sessionTime.Length == 4 && sessionTime.IndexOf(":", StringComparison.Ordinal) == 1) return "0" + sessionTime;

            var times = sessionTime.Split(':');
            var sb = new StringBuilder();
            foreach (var time in times)
            {
                if (time.Length == 1)
                    sb.AppendFormat("0{0}", time);
                else
                    sb.Append(time);
                sb.Append(":");
            }
            return sb.ToString().Trim(':');
        }

        public static Showtime CheckShowtimePresent(Showtime showtime, MovieScheduleStatsEntities model)
        {
            var updated = TryUpdateExistingShowTimeForTheSpecifiedTargetSite(showtime, model);

            if (updated) return null;

            var storedShowTimes = model.Showtimes.Where(x => x.CinemaId == showtime.CinemaId
                                                        && x.MovieId == showtime.MovieId
                                                        && x.Date == showtime.Date
                //&& x.TargetSiteId != showtime.TargetSiteId
                                                        ).ToList();

            //Special case to add 4D override
            if (showtime.SessionsFormat == SessionFormat.FourDX.ToString())
            {
                foreach (var storedShowTime in storedShowTimes.Where(x => x.SessionsFormat != SessionFormat.FourDX.ToString()))
                {
                    storedShowTime.SessionsCollection.RemoveAll(showtime.SessionsCollection.Contains);
                    storedShowTime.SessionsCollection = storedShowTime.SessionsCollection;
                }

                return showtime;
            }
            //Temporal measure to check how it will work with intersections
            return showtime;
            //return GetShowtimeSessionsDifference(showtime, storedShowTimes);
        }

        private static Showtime GetShowtimeSessionsDifference(Showtime showtime, List<Showtime> storedShowTimes)
        {
            int oirignalCount = showtime.SessionsCollection.Count;
            foreach (var storedShowTime in storedShowTimes)
            {
                showtime.SessionsCollection = showtime.SessionsCollection.Except(storedShowTime.SessionsCollection).ToList();
            }
            Showtime checkShowtime = null;
            if (showtime.SessionsCollection.Count > 0)
            {
                ParseRunProvider.Instance.IncrementNewShowtimesCount(showtime.TargetSiteId);
                checkShowtime = showtime;
                checkShowtime.PartialyAdded = oirignalCount != showtime.SessionsCollection.Count;
            }
            return checkShowtime;
        }

        private static Showtime GetShowtimeSessionsDifference(Showtime showtime, Showtime storedShowTime)
        {
            int oirignalCount = showtime.SessionsCollection.Count;
            var difference = showtime.SessionsCollection.Except(storedShowTime.SessionsCollection).ToList();

            Showtime checkShowtime = null;
            if (difference.Count > 0)
            {
                ParseRunProvider.Instance.IncrementNewShowtimesCount(showtime.TargetSiteId);
                checkShowtime = new Showtime()
                {
                    CinemaId = showtime.CinemaId,
                    MovieId = showtime.MovieId,
                    CityId = showtime.CityId,
                    ParseRunId = showtime.ParseRunId
                };
                checkShowtime.SessionsCollection = difference;
                checkShowtime.PartialyAdded = oirignalCount != difference.Count;
            }
            return checkShowtime;
        }

        public static bool TryUpdateExistingShowTimeForTheSpecifiedTargetSite(Showtime showtime, MovieScheduleStatsEntities model)
        {
            var storedShowTime = model.Showtimes.FirstOrDefault(x => x.CinemaId == showtime.CinemaId &&
                                                        x.MovieId == showtime.MovieId &&
                                                        x.Date == showtime.Date &&
                                                        x.TargetSiteId == showtime.TargetSiteId &&
                                                        x.SessionsFormat == showtime.SessionsFormat);
            if (storedShowTime == null) return false;

            if (storedShowTime.SessionsRaw.Equals(showtime.SessionsRaw))
            {
                storedShowTime.ParseRunId = ParseRunProvider.Instance.GetParseRun().Id;
                model.SaveChanges();
                return true;
            }

            //var adjustedSessionsCollection = showtime.SessionsCollection.Intersect(storedShowTime.SessionsCollection).ToList();

            var t = GetShowtimeSessionsDifference(showtime, storedShowTime);
            if (t == null || t.SessionsCount == 0) return true;

            //var storedShowTimes = model.Showtimes.Where(x => x.CinemaId == showtime.CinemaId &&
            //                                    x.MovieId == showtime.MovieId &&
            //                                    x.Date == showtime.Date &&
            //                                    x.Id != storedShowTime.Id).ToList();
            //var tt = GetShowtimeSessionsDifference(t, storedShowTimes);

            //if (tt != null)
            //    adjustedSessionsCollection.AddRange(tt.SessionsCollection);

            //if (adjustedSessionsCollection.Count == 0)
            //{
            //    model.Showtimes.Remove(storedShowTime);
            //    model.SaveChanges();
            //    return true;
            //}

            if (storedShowTime.ParseRunId != showtime.ParseRunId)
                ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(showtime.TargetSiteId);

            //storedShowTime.SessionsCollection = adjustedSessionsCollection.OrderBy(x => x).ToList();
            storedShowTime.SessionsRaw = showtime.SessionsRaw;
            storedShowTime.SessionsCount = showtime.SessionsCount;
            storedShowTime.LastUpdate = DateTime.Now;
            storedShowTime.ParseRunId = ParseRunProvider.Instance.GetParseRun().Id;
            model.SaveChanges();
            return true;
        }

        public static bool TryUpdateExistingShowTime(Showtime showtime, MovieScheduleStatsEntities model)
        {
            var storedShowTime = model.Showtimes.FirstOrDefault(x => x.CinemaId == showtime.CinemaId &&
                                                        x.MovieId == showtime.MovieId &&
                                                        x.Date == showtime.Date &&
                                                        x.TargetSiteId == showtime.TargetSiteId &&
                                                        x.SessionsFormat == showtime.SessionsFormat);
            if (storedShowTime != null)
            {
                if (storedShowTime.SessionsRaw != showtime.SessionsRaw)
                {
                    var adjustedSessionsCollection = showtime.SessionsCollection.Intersect(storedShowTime.SessionsCollection).ToList();

                    var t = GetShowtimeSessionsDifference(showtime, storedShowTime);
                    if (t == null || t.SessionsCount == 0) return false;

                    var storedShowTimes = model.Showtimes.Where(x => x.CinemaId == showtime.CinemaId &&
                                                        x.MovieId == showtime.MovieId &&
                                                        x.Date == showtime.Date &&
                                                        x.Id != storedShowTime.Id).ToList();
                    var tt = GetShowtimeSessionsDifference(t, storedShowTimes);

                    if (tt != null)
                        adjustedSessionsCollection.AddRange(tt.SessionsCollection);

                    if (adjustedSessionsCollection.Count == 0)
                    {
                        model.Showtimes.Remove(storedShowTime);
                        model.SaveChanges();
                        return true;
                    }

                    if (storedShowTime.ParseRunId != showtime.ParseRunId)
                        ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(showtime.TargetSiteId);

                    storedShowTime.SessionsCollection = adjustedSessionsCollection.OrderBy(x => x).ToList();
                    storedShowTime.LastUpdate = DateTime.Now;
                    storedShowTime.ParseRunId = ParseRunProvider.Instance.GetParseRun().Id;
                    model.SaveChanges();
                    return true;
                }
            }
            return false;
        }

        public static bool SkipNotCurrentCityCinema(Link cinemaLink, MovieScheduleStatsEntities model)
        {
            bool skip = false;
            if (cinemaLink.Text.Contains('(') || cinemaLink.Text.Contains(')'))
            {
                var cityName = cinemaLink.Text.GetValueInBrackets();
                if (model.Cities.Any(x => x.Name == cityName))
                {
                    new Logger().GetDefaultLogger().InfoFormat("Skipping not current city {0}, {1}", cityName, cinemaLink.Text);
                    skip = true;
                }
            }
            return skip;
        }


        public static void TryAddSnapshot(TargetSite ts, MovieScheduleStatsEntities model, Movie movie, Cinema cinema, DateTime showTimeDate, Showtime showtime, string format)
        {
            Showtime storedShowtime = showtime;
            if (showtime == null)
                storedShowtime = model.Showtimes.FirstOrDefault(x => x.MovieId == movie.Id &&
                                                                     x.CinemaId == cinema.Id &&
                                                                     x.Date == showTimeDate.Date &&
                                                                     x.SessionsFormat == format &&
                                                                     x.TargetSiteId == ts.Id);
            if (storedShowtime != null)
                TryAddSnapshotSingle(model, storedShowtime);
        }

        public static void TryAddSnapshots()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                //var currentParseRun = ParseRunProvider.Instance.GetParseRun(model);
                //DateTime today = DateTime.Today;
                DateTime releaseDate = new DateTime(2014, 10, 30);
                DateTime today = new DateTime(2014, 11, 6);

                var showtimes = model.Showtimes.Where(x => x.Date == today && x.Movie.ReleaseDate == releaseDate)
                    //.Select(x => new { CinemaId = x.CinemaId, MovieId = x.MovieId, Date = x.Date, TargetSiteId = x.TargetSiteId, SessionsFormat = x.SessionsFormat })
                    .Distinct().ToList();
                //var sht = model.Showtimes.Where(x => x.Date == today && x.Movie.ReleaseDate == releaseDate).Select(x => new { CinemaId = x.CinemaId, MovieId = x.MovieId, Date = x.Date, TargetSiteId = x.TargetSiteId, SessionsFormat = x.SessionsFormat }).Distinct().ToList();
                //var showtimes = sht.Select(
                //    x =>
                //        new Showtime
                //        {
                //            CinemaId = x.CinemaId,
                //            MovieId = x.MovieId,
                //            Date = x.Date,
                //            TargetSiteId = x.TargetSiteId,
                //            SessionsFormat = x.SessionsFormat
                //        }).ToList();
                //List<Showtime> showtimes = model.Showtimes.Where(x => x.Date == today && x.Movie.Title == "СЕРЕНА").ToList();
                foreach (var showtime in showtimes)
                {
                    TryAddSnapshotPostProcessing(model, showtime, today);
                }
            }
        }

        public static void TryAddSnapshotPostProcessing(MovieScheduleStatsEntities model, Showtime showtime, DateTime runDate)
        {
            var latestSnapshot = model.ShowtimeSnapshots
                .OrderByDescending(x => x.Date)
                .FirstOrDefault(x => x.MovieId == showtime.MovieId &&
                                    x.CinemaId == showtime.CinemaId &&
                                    x.TargetSiteId == showtime.TargetSiteId &&
                                    x.SessionsFormat == showtime.SessionsFormat);

            var releaseDate = showtime.Movie.ReleaseDate.Date;
            var previewDate = showtime.Movie.PreviewDate ?? showtime.Movie.ReleaseDate.Date;

            bool isReleaseDate = (showtime.Date == releaseDate || showtime.Date == previewDate) && (runDate.Date == releaseDate || runDate.Date == previewDate);
            bool isReleaseDate7Days = (latestSnapshot != null && (runDate.DayOfWeek == DayOfWeek.Thursday && runDate.Subtract(latestSnapshot.Date).Days >= 7));

            var snapshotExists = model.ShowtimeSnapshots
                .Any(x => x.MovieId == showtime.MovieId &&
                        x.CinemaId == showtime.CinemaId &&
                        x.Date == showtime.Date &&
                        x.SessionsFormat == showtime.SessionsFormat
                //&& x.TargetSiteId == showtime.TargetSiteId
                        );


            if ((!isReleaseDate && !isReleaseDate7Days) || snapshotExists) return;

            var showtimes = model.Showtimes.Include(x => x.TargetSite)
              .OrderByDescending(x => x.Date)
              .Where(x => x.Date == showtime.Date &&
                  x.MovieId == showtime.MovieId &&
                  x.CinemaId == showtime.CinemaId &&
                  x.SessionsFormat == showtime.SessionsFormat).ToList();

            var intersectedShowtime = GetIntersectedSessions(showtimes);

            ShowtimeSnapshot newSnapshot = new ShowtimeSnapshot();
            newSnapshot.TargetSiteId = intersectedShowtime.TargetSiteId;
            newSnapshot.ParseRunId = 610;//ParseRunProvider.Instance.GetParseRun(model).Id;
            newSnapshot.MovieId = intersectedShowtime.MovieId;
            newSnapshot.CinemaId = intersectedShowtime.CinemaId;
            newSnapshot.Date = showtime.Date;
            newSnapshot.SessionsCount = intersectedShowtime.SessionsCount;
            newSnapshot.Sessions = intersectedShowtime.SessionsRaw;
            newSnapshot.SessionsFormat = showtime.SessionsFormat;
            model.ShowtimeSnapshots.Add(newSnapshot);
            model.SaveChanges();
        }

        public static void TryAddSnapshotSingle(MovieScheduleStatsEntities model, Showtime showtime)
        {
            DateTime checkDate = DateTime.Now;
            if (showtime.Date != checkDate.Date) return;

            var latestSnapshot = model.ShowtimeSnapshots
                .OrderByDescending(x => x.Date)
                .FirstOrDefault(x => x.MovieId == showtime.MovieId &&
                                    x.CinemaId == showtime.CinemaId &&
                                    x.SessionsFormat == showtime.SessionsFormat &&
                                    x.TargetSiteId == showtime.TargetSiteId);

            #region Old implementation

            //var snapshotExists = model.ShowtimeSnapshots
            //    .Any(x => x.MovieId == movie.Id &&
            //            x.CinemaId == cinema.Id &&
            //            x.Date == showtime.Date &&
            //            x.TargetSiteId == ts.Id);

            //var releaseDate = movie.ReleaseDate.Date;
            //var previewDate = movie.PreviewDate ?? movie.ReleaseDate.Date;


            //bool isReleaseDate = (showtime.Date == releaseDate || showtime.Date == previewDate) && (checkDate.Date == releaseDate || checkDate.Date == previewDate);
            //bool isReleaseDate7Days = (latestSnapshot != null && (checkDate.DayOfWeek == DayOfWeek.Thursday && checkDate.Subtract(latestSnapshot.Date).Days >= 7));

            #endregion

            if (latestSnapshot != null && latestSnapshot.Date == checkDate.Date) return;
            //if (latestSnapshot != null && latestSnapshot.Date.Subtract(checkDate).Days < 1) return;

            var newSnapshot = new ShowtimeSnapshot
            {
                TargetSiteId = showtime.TargetSiteId,
                ParseRunId = ParseRunProvider.Instance.GetParseRun(model).Id,
                MovieId = showtime.MovieId,
                CinemaId = showtime.CinemaId,
                Date = showtime.Date,
                SessionsCount = showtime.SessionsCount,
                SessionsFormat = showtime.SessionsFormat,
                Sessions = showtime.SessionsRaw,
            };
            model.ShowtimeSnapshots.Add(newSnapshot);
        }

        public static void TryAddSnapshot(MovieScheduleStatsEntities model, Movie movie, Cinema cinema, Showtime showtime, TargetSite ts)
        {
            var snapshot = model.ShowtimeSnapshots
                .OrderByDescending(x => x.Date)
                .FirstOrDefault(x => x.MovieId == movie.Id &&
                                    x.CinemaId == cinema.Id &&
                                    x.Date == showtime.Date &&
                                    x.TargetSiteId == ts.Id);

            var releaseDate = movie.ReleaseDate.Date;
            var previewDate = movie.PreviewDate ?? movie.ReleaseDate.Date;
            bool isReleaseDate = (showtime.Date == releaseDate || showtime.Date == previewDate) && (DateTime.Now.Date == releaseDate || DateTime.Now.Date == previewDate);
            bool isReleaseDate7Days = (snapshot != null && (DateTime.Now.DayOfWeek == DayOfWeek.Thursday && DateTime.Now.Subtract(snapshot.Date).Days >= 7));

            if (isReleaseDate || isReleaseDate7Days)
            {
                bool requireAdd = (isReleaseDate && snapshot == null) || isReleaseDate7Days;

                if (snapshot == null || isReleaseDate7Days)
                {
                    snapshot = new ShowtimeSnapshot
                    {
                        TargetSiteId = ts.Id,
                        ParseRunId = ParseRunProvider.Instance.GetParseRun(model).Id
                    };
                }

                if (snapshot.SessionsCount > showtime.SessionsCount) return;

                if (showtime.SessionsCount > snapshot.SessionsCount ||
                    (showtime.SessionsCount == snapshot.SessionsCount && showtime.SessionsRaw != snapshot.Sessions))
                    snapshot.ParseRunId = ParseRunProvider.Instance.GetParseRun(model).Id;

                snapshot.MovieId = movie.Id;
                snapshot.CinemaId = cinema.Id;
                snapshot.Date = showtime.Date;
                snapshot.SessionsCount = showtime.SessionsCount;
                snapshot.Sessions = showtime.SessionsRaw;
                if (requireAdd)
                    model.ShowtimeSnapshots.Add(snapshot);
            }
        }

        public static Showtime GetIntersectedSessions(List<Showtime> showtimes)
        {
            if (showtimes.Count == 1)
            {
                showtimes[0].SessionsCollection = showtimes[0].SessionsCollection;
                return showtimes[0];
            }

            var maxPriority = showtimes.Max(x => x.TargetSite.Priority);
            if (maxPriority > 1)
            {
                Showtime priorityShowtime = showtimes.First(x => x.TargetSite.Priority == maxPriority);
                priorityShowtime.SessionsCollection = priorityShowtime.SessionsCollection;
                return priorityShowtime;
            }

            Showtime first = showtimes.First();
            List<string> result = first.SessionsCollection;

            foreach (var showtime in showtimes)
            {
                result = result.Intersect(showtime.SessionsCollection).ToList();
            }
            Showtime st = result.Count == 0 ?
                //showtimes.Aggregate((st1, st2) => st1.SessionsCount > st2.SessionsCount ? st1 : st2) :
                //showtimes.OrderByDescending(x => (x.LastUpdate ?? x.CreationDate)).First() :
                showtimes.OrderByDescending(x => x.SessionsCount).First() :
                new Showtime
                {
                    SessionsCollection = result,

                    Movie = first.Movie,
                    MovieId = first.MovieId,

                    Cinema = first.Cinema,
                    CinemaId = first.CinemaId,

                    TargetSite = first.TargetSite,
                    TargetSiteId = first.TargetSiteId,
                };

            //st.SessionsCollection = st.SessionsCollection.OrderBy(x => x, DataHelper.SessionsComparer).ToList();

            return st;
        }

        public static ShowtimeSnapshot GetIntersectedSnapshots(List<ShowtimeSnapshot> snapshots)
        {
            if (snapshots.Count == 1)
                return snapshots[0];
            ShowtimeSnapshot first = snapshots.First();
            List<string> result = snapshots.Aggregate(first.SessionsCollection, (current, showtime) => current.Intersect(showtime.SessionsCollection).ToList());

            ShowtimeSnapshot st = result.Count == 0 ?
                snapshots.OrderByDescending(x => x.SessionsCount).First() :
                new ShowtimeSnapshot
                {
                    SessionsCollection = result,

                    Movie = first.Movie,
                    MovieId = first.MovieId,

                    Cinema = first.Cinema,
                    CinemaId = first.CinemaId,

                    TargetSite = first.TargetSite,
                    TargetSiteId = first.TargetSiteId,
                };

            return st;
        }

        public static void PopulateSnapshots(int parseRunId = 0)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                if (parseRunId == 0)
                    parseRunId = ParseRunProvider.Instance.GetParseRun(model).Id;
                var group = model.Showtimes.Include(x => x.TargetSite).Where(x => x.ParseRunId == parseRunId).GroupBy(x => new
                {
                    x.MovieId,
                    x.CinemaId,
                    x.Date,
                    x.SessionsFormat
                }).ToList();

                foreach (var g in @group)
                {
                    int count = g.Count();
                    Showtime sht = count == 1
                        ? g.First()
                        : GetIntersectedSessions(g.ToList());
                    if (sht == null) continue;

                    TryAddSnapshotSingle(model, sht);
                }
            }
        }
    }

    public delegate Source SourceForCinema(Cinema cinema, City city, Link cinemaLink, string originalCity);
}
