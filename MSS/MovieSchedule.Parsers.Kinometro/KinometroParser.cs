using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using MovieSchedule.Core.Logging;
using MovieSchedule.Core.Managers;
using MovieSchedule.Data;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Parsers.Kinopoisk;
using Newtonsoft.Json.Linq;

namespace MovieSchedule.Parsers.Kinometro
{
    public class KinometroParser : ILoggable
    {
        private class URLs
        {
            public static readonly Uri Base = new Uri("http://www.kinometro.ru/");
            public const string Distributors = "http://www.kinometro.ru/distributor/";
            public const string Releases = "http://www.kinometro.ru/release";
            public const string Releases3D = "http://www.kinometro.ru/release/d3";
            public const string Analytics = "http://www.kinometro.ru/kino/analitika";
            //public const string AnalyticsJsonFormat = "http://www.kinometro.ru/kino/copytotal/page/{0}/start/{1}/limit/{2}/";
            //public const string AnalyticsJsonFormat = "http://www.kinometro.ru/kino/copytotal/_dc/1411582415428/page/{0}/start/{1}/limit/{2}/sort/[{\"property\":\"st_date_start\",\"direction\":\"DESC\"}]/filter/[{\"property\":\"ystart\",\"value\":2004},{\"property\":\"yend\",\"value\":2014}]";
            public const string AnalyticsJsonFormat = "http://www.kinometro.ru/kino/copytotal/_dc/{3}/page/{0}/start/{1}/limit/{2}/sort/%5B%7B%22property%22:%22st_date_start%22,%22direction%22:%22DESC%22%7D%5D/filter/%5B%7B%22property%22:%22ystart%22,%22value%22:2004%7D,%7B%22property%22:%22yend%22,%22value%22:2014%7D%5D";
            public const string MovieFormat = "http://www.kinometro.ru/release/card/id/{0}";
        }

        private List<Movie> _foundMovies = new List<Movie>();

        private static DateTime TryGetAdditionalDate(string text)
        {
            var result = new DateTime();
            var reg = new Regex(@"[/(]{1}[ПН|ВТ|СР|ЧТ|ПТ|СБ|ВС]{2}[\s]{1}([0-9/.]{8})[/)]{1}", RegexOptions.Compiled);
            var match = reg.Match(text);
            if (match.Success)
            {
                DateTime.TryParseExact(match.Groups[1].Value, "dd.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
            }
            return result;
        }

        private static Movie ParseMovie(HtmlNodeCollection cells, DateTime releaseDate)
        {
            var movieRef = cells[0].SelectSingleNode("a");
            var movieURL = movieRef.Attributes["href"].Value;
            movieURL = new Uri(URLs.Base, movieURL).ToString();
            var movieTitle = HttpUtility.HtmlDecode(cells[0].InnerText);
            var movie = ParseMovieInternal(movieTitle, movieURL);

            var additionalDate = TryGetAdditionalDate(movieTitle);
            if (additionalDate == DateTime.MinValue)
            {
                movie.ReleaseDate = releaseDate;
                return movie;
            }

            if (additionalDate > releaseDate)
                movie.ReleaseDate = additionalDate;
            else
            {
                movie.ReleaseDate = releaseDate;
                movie.PreviewDate = additionalDate;
            }

            return movie;
        }

        private static Movie ParseMovieInternal(string movieTitle, string movieURL, string movieOriginalTitle = "")
        {
            var movie = new Movie
                {
                    URL = movieURL,
                    Title = movieTitle,
                    Format = "2D",
                    CreationDate = DateTime.Now,
                };

            if (!string.IsNullOrWhiteSpace(movieOriginalTitle))
            {
                movie.Title = movieTitle.Replace(movieOriginalTitle, string.Empty).Trim(' ', '/');
                movie.OriginalTitle = movieOriginalTitle;
            }
            else if ((movieTitle.Contains("/") || movieTitle.Contains("(") || movieTitle.Contains("3Д") || movieTitle.Contains("3D")))
            {
                ParseMovieCard(movie);
            }
            if (string.IsNullOrWhiteSpace(movie.OriginalTitle) || !movie.OriginalTitle.Contains("("))
                movie.Title = RemoveOldRussianTitle(movie.Title);

            return movie;
        }

        private static void ParseMovieCard(Movie movie)
        {
            string movieTitle;
            var movieSource = HttpSender.GetHtmlResponse(movie.URL, encoding: Encoding.UTF8);
            var movieDoc = new HtmlDocument();
            movieDoc.LoadHtml(movieSource.Content);
            var titleNode = movieDoc.DocumentNode.SelectSingleNode("//p[@class='ftitle']");
            var rows = movieDoc.DocumentNode.SelectNodes("//table[@class='tcard']/tr");
            foreach (var row in rows)
            {
                if (row.InnerText.Contains("Дата начала проката в России"))
                {
                    var releaseDateNode = row.SelectSingleNode("td[2]");
                    DateTime result;
                    if (DateTime.TryParseExact(releaseDateNode.TrimDecode(), "dd.MM.yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out result))
                    {
                        movie.ReleaseDate = result;
                    }
                }
                if (row.InnerText.Contains("Носитель"))
                {
                    if (row.InnerText.Contains("3Д") || row.InnerText.Contains("3D"))
                        movie.Format = "3D";
                    break;
                }
            }

            movieTitle = titleNode.InnerText;
            var originalNode = titleNode.SelectSingleNode("span");
            if (originalNode != null)
            {
                movie.OriginalTitle = originalNode.TrimDecode();
                movie.Title = movieTitle.Replace(movie.OriginalTitle, string.Empty).Trim(' ', '/');
            }
        }

        private static string RemoveOldRussianTitle(string movieTitle)
        {
            var regex = new Regex(@"[\(][\w\W]+[\)]", RegexOptions.Compiled);
            var match = regex.Match(movieTitle);
            if (match.Success)
            {
                movieTitle = movieTitle.Replace(match.Value, string.Empty).Trim();
            }
            return movieTitle;
        }

        private List<Distributor> ParseDistributors(HtmlNode cell)
        {
            var result = new List<Distributor>();
            if (cell.ChildNodes.Count == 1)
            {
                var texts = HttpUtility.HtmlDecode(cell.InnerText).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                result.AddRange(texts.Select(text => new Distributor
                    {
                        CreationDate = DateTime.Now,
                        LastUpdate = DateTime.Now,
                        Name = text.Trim(),
                        Version = 1
                    }));
                return result;
            }

            foreach (var distrNode in cell.ChildNodes)
            {
                var nodeText = HttpUtility.HtmlDecode(distrNode.InnerText).Trim(' ', '/');
                if (string.IsNullOrWhiteSpace(nodeText)) continue;
                var distributor = new Distributor
                    {
                        CreationDate = DateTime.Now,
                        LastUpdate = DateTime.Now,
                        Name = nodeText,
                        Version = 1
                    };
                if (distrNode.Name == "a")
                {
                    distributor.Url = new Uri(URLs.Base, distrNode.Attributes["href"].Value).ToString();
                }
                result.Add(distributor);
            }

            return result;
        }

        private void Parse(string url)
        {
            var response = HttpSender.GetHtmlResponse(url, encoding: Encoding.UTF8);
            var doc = new HtmlDocument();

            doc.LoadHtml(response.Content);
            var root = doc.DocumentNode;

            var tableNode = root.SelectSingleNode("//table[@class='sr_tbl']");
            var rows = tableNode.SelectNodes("tr");
            if (rows != null)
            {
                DateTime releaseDate = DateTime.Now;
                int current = 0;
                int failed = 0;
                foreach (var row in rows)
                {
                    current++;
                    try
                    {
                        //Console.WriteLine(row.OuterHtml);
                        if (row.Attributes.Contains("class") && row.Attributes["class"].Value == "rel_date_row")
                        {
                            var dateRaw = row.SelectSingleNode("td").InnerText;
                            releaseDate = DateTime.Parse(dateRaw, CultureInfo.GetCultureInfo("ru-RU"));
                            continue;
                        }

                        var cells = row.SelectNodes("td");

                        var movie = ParseMovie(cells, releaseDate);

                        var distributors = ParseDistributors(cells[2]);

                        using (var model = new MovieScheduleStatsEntities())
                        {
                            var distributorsToAdd = new List<Distributor>();
                            foreach (var distributor in distributors)
                            {
                                var storedDistributor = model.Distributors.FirstOrDefault(x => distributor.Name == x.Name);
                                if (storedDistributor == null)
                                {
                                    model.Distributors.Add(distributor);
                                    distributorsToAdd.Add(distributor);
                                }
                                else
                                {
                                    distributorsToAdd.Add(storedDistributor);
                                }
                            }


                            var storedMovie = model.Movies.FirstOrDefault(x => x.URL == movie.URL);
                            if (storedMovie != null)
                            {
                                if (storedMovie.ReleaseDate != movie.ReleaseDate)
                                {
                                    storedMovie.ReleaseDate = movie.ReleaseDate;
                                    this.GetDefaultLogger().InfoFormat("Updated relasedate '{0}' -> '{1}' ({2})", storedMovie.Title, storedMovie.ReleaseDate, movie.ReleaseDate, movie.URL);
                                }
                                if (storedMovie.Title != movie.Title)
                                {
                                    storedMovie.Title = movie.Title;
                                    this.GetDefaultLogger().InfoFormat("Updated '{0}' -> '{1}' ({2})", storedMovie.Title, movie.Title, movie.URL);
                                }
                                if (storedMovie.OriginalTitle != movie.OriginalTitle)
                                {
                                    storedMovie.OriginalTitle = movie.OriginalTitle;
                                    this.GetDefaultLogger().InfoFormat("Updated '{0}' -> '{1}' ({2})", storedMovie.OriginalTitle, movie.OriginalTitle, movie.URL);
                                }
                                model.SaveChanges();
                                _foundMovies.Add(storedMovie);
                                continue;
                            }

                            foreach (var distributor in distributorsToAdd)
                            {
                                movie.Distributors.Add(distributor);
                            }
                            model.Movies.Add(movie);
                            model.SaveChanges();
                            _foundMovies.Add(movie);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.WriteLine(ex.ToString());
                    }
                    Console.WriteLine("Out of {0}, {1} failed", current, failed);
                }
            }
        }

        public void ParseDistributors()
        {
            var response = HttpSender.GetHtmlResponse(URLs.Distributors, encoding: Encoding.UTF8);
            var doc = new HtmlDocument();

            doc.LoadHtml(response.Content);
            var root = doc.DocumentNode;
            using (var model = new MovieScheduleStatsEntities())
            {
                var distrNodes = root.SelectNodes("//dd");
                foreach (var distrNode in distrNodes)
                {
                    var distributor = new Distributor { CreationDate = DateTime.Now, Version = 1, LastUpdate = DateTime.Now };
                    var link = distrNode.SelectSingleNode("a");
                    if (link != null)
                    {
                        distributor.Url = new Uri(URLs.Base, link.Attributes["href"].Value).ToString();
                        if (model.Distributors.Any(x => x.Url == distributor.Url)) continue;
                        distributor.Name = link.TrimDecode();

                        var param = link.NextSibling.NextSibling.TrimDecode();
                        distributor.Info = param;
                    }
                    else
                    {
                        distributor.Name = distrNode.TrimDecode();
                        distributor.Info = distributor.Name;
                        if (model.Distributors.Any(x => x.Name == distributor.Name || x.DisplayName == distributor.Name)) continue;
                    }
                    model.Distributors.Add(distributor);
                    model.SaveChanges();
                }
            }
        }

        public void ParseAnalytics()
        {
            var initialResponse = HttpSender.GetHtmlResponse(URLs.Analytics, encoding: Encoding.UTF8);
            var headers = Defaults.Headers.Clone();
            if (Defaults.Headers.Cookies.Count > 0)
            {
                headers.Cookies = Defaults.Headers.Cookies;
            }
            else
            {
                headers.Cookies = new CookieContainer();
                headers.Cookies.Add(initialResponse.ResponseCookies);
            }
            headers.Referer = URLs.Analytics;
            int page = 1;
            int skip = 0;
            int pageSize = 100;
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            var response = HttpSender.GetHtmlResponse(string.Format(URLs.AnalyticsJsonFormat, page, skip, pageSize, unixTimestamp), headers: headers, encoding: Encoding.UTF8);

            dynamic list = JObject.Parse(response.Content);

            int total = list.total;
            bool success = list.success;

            int pages = (int)Math.Ceiling((decimal)(total / pageSize));
            if (success)
            {
                ParseMovieFromJson(list);
                skip += pageSize;
                for (int i = 2; i < pages; i++)
                {
                    response = HttpSender.GetHtmlResponse(string.Format(URLs.AnalyticsJsonFormat, i, skip, pageSize, unixTimestamp), headers: headers, encoding: Encoding.UTF8);
                    list = JObject.Parse(response.Content);
                    ParseMovieFromJson(list);
                    skip += pageSize;
                    SleepManager.SleepRandomTimeout();
                }
                FixOldMoviesFormats();
            }
        }

        public void FixOldMoviesFormats()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                //List<Movie> oldMovies = model.Movies.Where(x => x.ReleaseDate < DateTime.Now).ToList();
                List<Movie> oldMovies = model.Movies.Where(x => x.ReleaseDate >= DateTime.Now).ToList();
                foreach (var movie in oldMovies)
                {
                    ParseMovieCard(movie);
                    model.SaveChanges();
                }

            }
        }

        private void ParseMovieFromJson(dynamic list)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                foreach (var item in list.movie)
                {
                    string movieTitle = item.st_name_ru;
                    string movieOriginalTitle = item.st_name_en;
                    string movieId = item.st_rel_id;
                    string movieUrl = string.Format(URLs.MovieFormat, movieId);
                    string distributorShortcutLink = item.st_distrib;

                    var distributorLinks = distributorShortcutLink.Replace("<//a>//", "<//a>|").Replace("//<a", "|<a").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    var distributors = new List<Distributor>();
                    foreach (var distributorLink in distributorLinks)
                    {
                        if (!distributorLink.Contains("a"))
                        {
                            //if (distributorLink.Contains("//"))
                            {
                                foreach (var dl in distributorLink.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    var shortcut = dl.Trim();
                                    var distributor = model.Distributors.FirstOrDefault(x => x.Name == shortcut);
                                    if (distributor != null) distributors.Add(distributor);
                                    else
                                    {
                                        distributor = new Distributor
                                            {
                                                CreationDate = DateTime.Now,
                                                Version = 1,
                                                LastUpdate = DateTime.Now,
                                                Name = shortcut,
                                                Info = shortcut
                                            };
                                        distributors.Add(distributor);
                                        model.Distributors.Add(distributor);
                                    }
                                }
                            }
                        }
                        else
                        {
                            var r = new Regex("[<a href=/\"]{7}([a-zA-Z//]+)[\" ]{2}[a-zA/\"=_]+[>]{1}([a-zA-Z0-9_ ]+)[</a>]{4}");
                            var link = r.Match(distributorLink);
                            if (link.Success)
                            {
                                var href = link.Groups[1].Value;
                                var text = link.Groups[2].Value;
                                var uri = new Uri(URLs.Base, href).ToString();
                                var storedDistr = model.Distributors.FirstOrDefault(x => x.Url == uri);
                                //Assume we have all distributors stored
                                if (storedDistr != null)
                                {
                                    distributors.Add(storedDistr);
                                }
                            }
                        }
                    }

                    string startDate = item.st_date_start;
                    Movie movie = ParseMovieInternal(movieTitle, movieUrl, movieOriginalTitle);
                    var movieIsPresent = model.Movies.Any(x => x.Title == movie.Title && x.OriginalTitle == movie.OriginalTitle);
                    if (movieIsPresent)
                    {
                        this._foundMovies.Add(movie);
                        continue;
                    }

                    movie.ReleaseDate = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                    foreach (var distributor in distributors)
                    {
                        movie.Distributors.Add(distributor);
                    }

                    model.Movies.Add(movie);
                    model.SaveChanges();
                }
            }
        }

        public void ParseReleases()
        {
            Parse(URLs.Releases);
        }

        public void ParseReleases3D()
        {
            Parse(URLs.Releases3D);
        }

        public void Parse()
        {
            ParseDistributors();
            SleepManager.SleepRandomTimeout(3, 5);
            Parse(URLs.Releases);
            SleepManager.SleepRandomTimeout(3, 5);
            Parse(URLs.Releases3D);
            SuspendReleases();
        }

        public void SuspendReleases()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var dbMovies = model.Movies.Where(x => x.ReleaseDate > DateTime.Now).ToList();

                foreach (var item in _foundMovies.Select(movie => dbMovies.FirstOrDefault(x => x.URL == movie.URL)).Where(item => item != null))
                {
                    dbMovies.Remove(item);
                    if (item.Suspended)
                    {
                        item.Suspended = false;
                    }
                }

                foreach (var dbMovie in dbMovies)
                {
                    this.GetDefaultLogger().InfoFormat("Suspending {0}{1} ({2})", dbMovie.Title, dbMovie.OriginalTitle == null ? string.Empty : " / " + dbMovie.OriginalTitle, dbMovie.URL);
                    dbMovie.Suspended = true;
                }
                model.SaveChanges();
            }
        }
    }
}
