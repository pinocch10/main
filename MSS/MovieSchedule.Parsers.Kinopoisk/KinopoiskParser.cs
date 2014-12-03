using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Data.Entity;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MovieSchedule.Core;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Core.Logging;
using MovieSchedule.Core.Managers;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Networking;
using MovieSchedule.Parsers.Common;
using Newtonsoft.Json.Linq;
using Constants = MovieSchedule.Data.Helpers.Constants;

namespace MovieSchedule.Parsers.Kinopoisk
{
    public class KinopoiskParser : IMovieScheduleParser, ILoggable
    {
        #region Constructors

        public KinopoiskParser()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                TargetSite = model.TargetSites.FirstOrDefault(x => x.Shortcut == TargetSiteShortcut);
            }
        }

        #endregion

        #region Internal classes
        class CityItem
        {
            public string FixedName { get; set; }
            public string Name { get; set; }
            public string Region { get; set; }
            public string CityId { get; set; }
            public string Link { get; set; }
            public string Country { get; set; }

        }

        class URLs
        {
            public const string CinemasFormat = "http://www.kinopoisk.ru/cinemas/tc/{0}/perpage/200/";
            public const string ShowtimesFormat = "http://m.kinopoisk.ru/afisha/new/sort/num/day/{0:yyyy-MM-dd}";
            public const string ShowtimesFormatFull = "http://www.kinopoisk.ru/afisha/tc/{0}/";
            public const string ShowtimesFormatFullPerDay = "http://www.kinopoisk.ru/afisha/tc/{0}/day_view/{1:yyyy-MM-dd}/";
            public const string City = "http://m.kinopoisk.ru/";
            public const string Base = "http://www.kinopoisk.ru/";
            public const string CityFull = "http://www.kinopoisk.ru/afisha/new/";

            public const string MovieListFormat = "http://www.kinopoisk.ru/premiere/ru/{0}/";
            public const string MovieListMobileFormat = "http://m.kinopoisk.ru/premier/{0}-{1}/";

            public const string MovieFormat = "http://www.kinopoisk.ru/film/{0}/";
        }
        #endregion

        #region IMovieScheduleParser implementation

        public TargetSite TargetSite { get; private set; }

        public void GetMovieListWithDeviation()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var movieYears = model.Movies.Select(x => x.ReleaseDate.Year).Distinct().ToList();
                //var years = movieYears.OrderBy(x => x).ToList();
                var years = new List<int> { 2013 };
                //CheckDeviation("http://www.kinopoisk.ru/premiere/ru/", model);
                foreach (var movieYear in years)
                {
                    var movieListUrl = string.Format(URLs.MovieListFormat, movieYear);
                    CheckDeviation(movieListUrl, model);
                }
            }
        }

        public void FillMoviesDetails()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var movieYears = model.Movies.Select(x => x.ReleaseDate.Year).Distinct().ToList();
                var years = movieYears.OrderBy(x => x).ToList();
                CheckPremiersList("http://www.kinopoisk.ru/premiere/ru/", model);
                foreach (var movieYear in years)
                {
                    var movieListUrl = string.Format(URLs.MovieListFormat, movieYear);
                    CheckPremiersList(movieListUrl, model);
                }
            }
        }

        private void CheckPremiersList(string movieListUrl, MovieScheduleStatsEntities model)
        {
            var root = HttpSender.GetHtmlNodeResponse(movieListUrl);
            if (root == null) return;
            var links =
                root.SelectNodes("//div[@class='premier_item']/div[@class='text']/div[@class='textBlock']/span[@class='name']/a");
            if (links == null) return;
            foreach (var link in links)
            {
                string relativeUri = link.Attributes["href"].Value;
                var uri = new Uri(new Uri(URLs.Base), relativeUri);
                var movieTitle = link.TrimDecode();
                var movie = model.Movies.FirstOrDefault(x => x.Title == movieTitle);
                if (movie == null)
                    continue;
                string url = uri.ToString();
                var movieRoot = HttpSender.GetHtmlNodeResponse(url);
                var rows = movieRoot.SelectNodes(@"//div[@id='infoTable']/table[@class='info']/tr");

                var globalReleaseDate = GetMoviecardReleaseDate(rows, movie, url);
                if (globalReleaseDate == DateTime.MinValue) continue;
                movie.ReleaseDateGlobal = globalReleaseDate;
                if (!movie.MovieSources.Any(
                    x => x.URL == url && x.TargetSite.Shortcut == Constants.Kinopoisk))
                {
                    model.MovieSources.Add(new MovieSource
                    {
                        MovieId = movie.Id,
                        Parameter = relativeUri,
                        URL = url,
                        TargetSiteId = model.TargetSites.First(x => x.Shortcut == Constants.Kinopoisk).Id
                    });
                }
                model.SaveChanges();
            }
        }

        private string _movieToken;

        internal class UrlInfo
        {
            internal int Year { get; set; }
            internal int Month { get; set; }
            internal string Url { get; set; }
        }

        private int _threadsCount = 10;
        private Queue<UrlInfo> _urls;

        private int _exactmatch = 0;
        private int _deviationMatch = 0;

        public void ParseMovies(int threadsCount = 0)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var movieYears = model.Movies.Select(x => x.ReleaseDate.Year).Distinct().ToList();
                var years = movieYears.OrderBy(x => x).ToList();
                _urls = new Queue<UrlInfo>();
                foreach (var movieYear in years)
                {
                    for (int month = 1; month <= 12; month++)
                    {
                        var url = string.Format(URLs.MovieListMobileFormat, movieYear, month);
                        _urls.Enqueue(new UrlInfo() { Url = url, Year = movieYear, Month = month });
                    }
                }
                List<Task> _tasks = new List<Task>();
                if (threadsCount == 0)
                    threadsCount = _threadsCount;
                var sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < threadsCount; i++)
                {
                    var t = new Task(ParseMobilePremierMonth);
                    _tasks.Add(t);
                    t.Start();
                }
                Task.WaitAll(_tasks.ToArray());
                sw.Stop();
                Console.WriteLine("Parsing movies threads:{0} {1} exact: {2} deviated: {3}", threadsCount, sw.Elapsed, _exactmatch, _deviationMatch);
            }
        }

        private object _locker = new object();
        private Regex _regex = new Regex("[movie/]{6}([0-9]+)[/]{1}", RegexOptions.Compiled);

        private void ParseMobilePremierMonth()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                while (true)
                {
                    UrlInfo url = null;
                    lock (_locker)
                    {
                        if (_urls.Count == 0) return;
                        url = _urls.Dequeue();
                    }
                    if (url == null) return;
                    ParseMobilePremierMonth(url, model);
                }
            }
        }

        private void ParseMobilePremierMonth(UrlInfo info, MovieScheduleStatsEntities model)
        {
            var root = HttpSender.GetHtmlNodeResponse(info.Url);

            var links = root.SelectNodes("//div[@class='block prem']/p/a");
            if (links == null) return;
            foreach (var link in links)
            {
                string relativeUri = link.Attributes["href"].Value;
                var uri = new Uri(new Uri(URLs.Base), relativeUri);
                var movieTitle = link.TrimDecode();

                var movie = model.Movies.Include(x => x.MovieSources).FirstOrDefault(x => x.Title == movieTitle);
                if (movie != null)
                {
                    lock (_locker)
                    {
                        _exactmatch++;
                    }
                    continue;
                }
                var suggest = DataHelper.GetMovieSuggestions(movieTitle);
                var bestSuggestion = suggest.Suggestions.OrderByDescending(x => x.Simmilarity).FirstOrDefault();
                if (bestSuggestion == null)
                    continue;
                movie = model.Movies.First(x => x.Id == bestSuggestion.Id);
                if (movie.MovieSources.Any(x => x.TargetSite.Shortcut == Constants.Kinopoisk)) continue;
                if (Math.Abs(movie.ReleaseDate.Subtract(new DateTime(info.Year, info.Month, 1)).Days) > 40) continue;

                lock (_locker)
                {
                    _deviationMatch++;
                }
                var deviationString = string.Format("{5} {4}% ({1} / {2}) {3:yyyy-MM-dd}", movie.Id, movie.Title, movie.OriginalTitle, movie.ReleaseDate, bestSuggestion.Simmilarity, movieTitle);
                var match = _regex.Match(relativeUri);
                if (!match.Success)
                {
                    Console.WriteLine("Failed to parse link: {0}", relativeUri);
                    continue;
                }
                var url = string.Format(URLs.MovieFormat, match.Groups[1].Value);

                AddMovieSource(model, relativeUri, movie, relativeUri, deviationString);
            }
        }

        private void AddMovieSource(MovieScheduleStatsEntities model, string url, Movie movie, string relativeUri, string deviationLogMessage)
        {
            //string url = uri.ToString();
            var movieRoot = HttpSender.GetHtmlNodeResponse(url);
            var rows = movieRoot.SelectNodes(@"//div[@class='block film']/p/b");
            if (rows == null)
            {
                this.GetLogger("KinopoiskDataLogger").Info(movieRoot.OuterHtml);
                return;
            }
            var globalReleaseDate = GetMovieCardReleaseDateMobile(rows, movie, url);
            //var globalReleaseDate = GetMoviecardReleaseDate(rows, movie, url);
            if (globalReleaseDate == DateTime.MinValue) return;
            movie.ReleaseDateGlobal = globalReleaseDate;
            lock (_locker)
            {
                if (model.MovieSources.Any(x => x.MovieId == movie.Id
                                            && x.URL == url
                                            && x.TargetSite.Shortcut == Constants.Kinopoisk)) return;
                model.MovieSources.Add(new MovieSource
                {
                    MovieId = movie.Id,
                    Parameter = relativeUri,
                    URL = url,
                    TargetSiteId = model.TargetSites.First(x => x.Shortcut == Constants.Kinopoisk).Id
                });
                try
                {
                    model.SaveChanges();
                    if (!string.IsNullOrWhiteSpace(deviationLogMessage))
                        Console.WriteLine(deviationLogMessage);
                }
                catch
                {
                    try
                    {
                        if (model.MovieSources.Any(x => x.MovieId == movie.Id
                                           && x.URL == url
                                           && x.TargetSite.Shortcut == Constants.Kinopoisk)) return;
                        model.MovieSources.Add(new MovieSource
                        {
                            MovieId = movie.Id,
                            Parameter = relativeUri,
                            URL = url,
                            TargetSiteId = model.TargetSites.First(x => x.Shortcut == Constants.Kinopoisk).Id
                        });
                        if (!string.IsNullOrWhiteSpace(deviationLogMessage))
                            Console.WriteLine(deviationLogMessage);
                    }
                    catch (Exception ex)
                    {
                        this.GetDefaultLogger().Fatal(string.Format("{0} ({1}) {2}", movie.Title, movie.Id, url), ex);
                        this.GetDefaultLogger().Fatal(ex.Message);
                    }

                }
            }
        }

        private void CheckDeviation(string movieListUrl, MovieScheduleStatsEntities model)
        {
            var root = HttpSender.GetHtmlNodeResponse(movieListUrl);
            if (root == null) return;
            if (string.IsNullOrWhiteSpace(_movieToken))
            {
                var tokenNode = root.SelectSingleNode("//script[contains(text(),'xsrftoken')]");
                if (tokenNode != null)
                {
                    var source = tokenNode.InnerText;
                }
            }

            var links = root.SelectNodes("//div[@class='premier_item']/div[@class='text']/div[@class='textBlock']/span[@class='name']/a");
            if (links == null) return;
            foreach (var link in links)
            {
                string relativeUri = link.Attributes["href"].Value;
                var uri = new Uri(new Uri(URLs.Base), relativeUri);
                var movieTitle = link.TrimDecode();

                var movie = model.Movies.FirstOrDefault(x => x.Title == movieTitle);
                if (movie == null)
                {
                    var suggest = DataHelper.GetMovieSuggestions(movieTitle);
                    var bestSuggestion = suggest.Suggestions.OrderByDescending(x => x.Simmilarity).FirstOrDefault();
                    if (bestSuggestion == null)
                        continue;
                    movie = model.Movies.First(x => x.Id == bestSuggestion.Id);
                    Console.WriteLine("{0} {5} {4}% ({1} / {2}) {3:yyyy-MM-dd}",
                        movie.Id, movie.Title, movie.OriginalTitle, movie.ReleaseDate, bestSuggestion.Simmilarity, movieTitle);
                }
                //string url = uri.ToString();
                //var movieRoot = HttpSender.GetHtmlNodeResponse(url);
                //var rows = movieRoot.SelectNodes(@"//div[@id='infoTable']/table[@class='info']/tr");

                //var globalReleaseDate = GetMoviecardReleaseDate(rows, movie, url);
                //if (globalReleaseDate == DateTime.MinValue) continue;
                //movie.ReleaseDateGlobal = globalReleaseDate;
                //if (!movie.MovieSources.Any(
                //    x => x.URL == url && x.TargetSite.Shortcut == Constants.Kinopoisk))
                //{
                //    model.MovieSources.Add(new MovieSource
                //    {
                //        MovieId = movie.Id,
                //        Parameter = relativeUri,
                //        URL = url,
                //        TargetSiteId = model.TargetSites.First(x => x.Shortcut == Constants.Kinopoisk).Id
                //    });
                //}
                //model.SaveChanges();
            }
        }

        private DateTime GetMovieCardReleaseDateMobile(HtmlNodeCollection paragraphs, Movie movie, string movieUrl)
        {
            DateTime globalReleaseDate = new DateTime();
            foreach (var p in paragraphs)
            {
                var title = p.TrimDecode();
                var value = p.NextSibling.TrimDecode();
                var pIndex = value.IndexOf('(');
                if (pIndex >= 0)
                    value = value.Substring(0, pIndex);
                switch (title)
                {
                    case "премьера (мир):":
                        globalReleaseDate = DateTime.Parse(value, CultureInfo.GetCultureInfo("ru-RU"));

                        break;
                    case "премьера (РФ):":
                        DateTime russianReleaseDate = DateTime.Parse(value, CultureInfo.GetCultureInfo("ru-RU"));
                        if (movie.ReleaseDate.Subtract(russianReleaseDate).TotalDays > 10) return DateTime.MinValue;
                        break;
                    default: continue;
                }
            }
            return globalReleaseDate;
        }

        private DateTime GetMoviecardReleaseDate(HtmlNodeCollection rows, Movie movie, string movieUrl)
        {
            DateTime globalReleaseDate = new DateTime();
            DateTime russianReleaseDate = new DateTime();
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(@"td");
                //var typeCell = row.SelectNodes(@"td[class='type']");
                if (globalReleaseDate == DateTime.MinValue)
                    globalReleaseDate = GetMoviecardReleaseDate(cells, "премьера (мир)");
                if (russianReleaseDate == DateTime.MinValue)
                    russianReleaseDate = GetMoviecardReleaseDate(cells, "премьера (РФ)", @"div/span/a");
                if (globalReleaseDate == DateTime.MinValue || russianReleaseDate == DateTime.MinValue) continue;
                //if (movie.ReleaseDate.Subtract(russianReleaseDate).TotalDays > 31)
                if (movie.ReleaseDate.Subtract(russianReleaseDate).TotalDays > 10)
                {
                    this.GetDefaultLogger().InfoFormat("{0} skipping, release dates mismatch " +
                                "{1:yyyy-MM-dd} - {2:yyyy-MM-dd} {3}", movie.Title, movie.ReleaseDate, russianReleaseDate, movieUrl);
                    continue;
                }
                return globalReleaseDate;
            }
            return DateTime.MinValue;
        }

        private static DateTime GetMoviecardReleaseDate(HtmlNodeCollection cells, string releaseTitle, string xpath = @"div[@title='Дополнительная информация']/a")
        {
            if (cells == null || cells[0].TrimDecode() != releaseTitle) return DateTime.MinValue;
            var dateCell = cells[1].SelectSingleNode(xpath);
            return dateCell == null ?
                DateTime.MinValue :
                DateTime.Parse(dateCell.TrimDecode(), CultureInfo.GetCultureInfo("ru-RU"));
        }

        public void Parse()
        {
            var sw = new Stopwatch();
            sw.Start();
            //ParseCinemas();
            ParseCities();
            sw.Stop();
            Console.WriteLine("Parsed city in {0}", sw.Elapsed);
            sw.Restart();
            ParseShowtimes();
            sw.Stop();
            Console.WriteLine("Parsed showtimes in {0}", sw.Elapsed);
        }

        public void ParseCities()
        {
            var citiesSource = HttpSender.GetHtmlResponse(URLs.CityFull, encoding: Encoding);
            var doc = new HtmlDocument();
            doc.LoadHtml(citiesSource.Content);
            var root = doc.DocumentNode;
            var select = root.SelectSingleNode("//div[@class='city_block']");

            var scriptText = NormalizeCitiesJSON(@select);
            //scriptText = Regex.Unescape(scriptText);
            dynamic data = JObject.Parse(scriptText);
            Dictionary<string, string> allowedCountries = new Dictionary<string, string>();
            foreach (var country in data.country_data)
            {
                //if (country.Value == "Россия")
                allowedCountries.Add(country.Value.ToString(), country.Name);
            }
            foreach (var allowedCountry in allowedCountries)
            {
                var cities = new List<CityItem>();

                foreach (var c in data.city_data[allowedCountry.Value])
                {
                    var item = new CityItem()
                        {
                            CityId = c.Value.id_city,
                            Link = c.Value.link,
                            Name = c.Value.name,
                            Region = c.Value.region,
                            Country = allowedCountry.Key
                        };
                    item.FixedName = item.Name.Replace("ё", "е");
                    item.FixedName = item.FixedName.Replace("Ё", "Е");
                    cities.Add(item);
                    string cityId = c.Value.id_city.ToString();
                    //SaveCity(item, cityId);
                }

                foreach (var cityItem in cities)
                {
                    using (var model = new MovieScheduleStatsEntities())
                    {
                        SaveCity(cityItem, model, cities.FirstOrDefault(x => x.CityId == cityItem.Region));
                    }
                }
            }
        }

        private void SaveCity(CityItem item, MovieScheduleStatsEntities model, CityItem master = null)
        {
            var city = AddOrGetCity(item, model);
            if (master != null)
            {
                var masterCity = AddOrGetCity(master, model);
                city.SatelliteTo = masterCity;
                model.SaveChanges();
            }
        }

        private City AddOrGetCity(CityItem item, MovieScheduleStatsEntities model)
        {
            City result = null;
            result = model.Cities.Include("Sources").FirstOrDefault(x => x.Name == item.FixedName);
            if (result != null)
            {
                if (!result.Sources.Any(x => x.TargetSite == TargetSiteShortcut && x.Parameter.Contains(item.CityId)))
                {
                    var source = GenerateSourceForCity(result, item.CityId, item.Name);
                    result.Sources.Add(source);
                }
            }
            else
            {

                var country = model.Countries.FirstOrDefault(x => x.Name == item.Country);
                if (country == null)
                {
                    country = new Country()
                    {
                        Name = item.Country,
                        Code = "  "
                    };
                    model.Countries.Add(country);
                    model.SaveChanges();
                }

                result = new City
                    {
                        Name = item.FixedName,
                        Country = country
                    };
                var source = GenerateSourceForCity(result, item.CityId, item.Name);
                result.Sources.Add(source);
                model.Cities.Add(result);
            }
            model.SaveChanges();
            return result;
        }

        public Encoding Encoding
        {
            get { return Encoding.GetEncoding(1251); }
        }

        public string GetTargetSite()
        {
            return TargetSiteShortcut;
        }

        public string GetBaseCityUrl()
        {
            return URLs.City;
        }

        public Dictionary<string, List<Link>> ParseCinemas()
        {
            var result = new Dictionary<string, List<Link>>();
            var sw = new Stopwatch();
            sw.Start();
            List<City> cities;
            using (var model = new MovieScheduleStatsEntities())
            {
                cities = model.Cities.Include("Sources").Where(x => x.Sources.Any(xx => xx.TargetSite == TargetSiteShortcut)).ToList();
            }

            int pagesCount = 0;
            foreach (var city in cities)
            {
                try
                {
                    var response = HttpSender.GetHtmlResponse(string.Format(URLs.CinemasFormat, city.Sources.First().Parameter), encoding: Encoding);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(response.Content);
                    var root = doc.DocumentNode;

                    var names = root.SelectNodes("//a[@itemprop='name']");
                    var links = new List<Link>();
                    foreach (var name in names)
                    {
                        var link = name.ParseLink();
                        if (link == null)
                        {
                            Console.WriteLine("{0}->{1} missing link", city.Name, name.InnerText);
                            continue;
                        }
                        links.Add(link);
                    }
                    result.Add(city.Name, links);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    SleepManager.SleepRandomTimeout(1, 2, 1000);
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed {0} to parse {1} pages", sw.Elapsed, pagesCount);
            return result;
        }

        public void ParseShowtimes()
        {
            var cities = new List<City>();
            using (var model = new MovieScheduleStatsEntities())
            {
                cities = model.Cities.Include(x => x.Sources).Include(x => x.Satellites).Where(x => x.Sources.Any(xx => xx.TargetSite == TargetSiteShortcut && xx.MovieId == null && xx.CinemaId == null)).OrderBy(x => x.Name).ToList();
                //cities = cities.Where(x => x.Name == "Москва").ToList();
            }

            int i = 0;
            foreach (var city in cities)
            {
                try
                {
                    var citySource = city.Sources.First(x => x.TargetSite == TargetSiteShortcut && x.MovieId == null && x.CinemaId == null);
                    this.GetDefaultLogger().InfoFormat("kinopoisk. Parsing {0}({1})", city.Name, city.Id);
                    ParseShowtimesForCity(city, string.Format(URLs.ShowtimesFormatFull, citySource.Parameter));
                    //ParseShowtimesForCity(city, string.Format(URLs.ShowtimesFormatFullPerDay, citySource.Parameter, DateTime.Today.AddDays(3)), new List<string> { "Голодные игры: Сойка-пересмешница. Часть I" });
                    //ParseShowtimesForCity(city, string.Format(URLs.ShowtimesFormatFullPerDay, citySource.Parameter, DateTime.Today.AddDays(6)), new List<string> { "Голодные игры: Сойка-пересмешница. Часть I" });

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    i++;
                    //SleepManager.SleepRandomTimeout();
                }

            }
        }

        #endregion

        #region Constants

        public const string TargetSiteShortcut = "kinopoisk.ru";
        private const string ParamFormat = "/city/{0}/";

        #endregion

        #region Helper methods

        private static string NormalizeCitiesJSON(HtmlNode @select)
        {
            var sb = new StringBuilder(@select.NextSibling.NextSibling.InnerText.Trim());
            sb = sb.Replace("KPCity.init('.city_block',", string.Empty);
            sb = sb.Replace(");", string.Empty);
            sb = sb.Replace("var ur_data= [];", string.Empty);
            sb = sb.Replace('\r', ' ');
            sb = sb.Replace('\n', ' ');
            sb = sb.Replace('\t', ' ');
            //sb = sb.Replace(" ", string.Empty);
            var scriptText = sb.ToString();
            return scriptText;
        }

        #endregion

        #region Private methods

        private void ParseShowtimesForCity(City city, string url, List<string> optionalMovies = null)
        {
            var showtimesSource = HttpSender.GetHtmlResponse(url, encoding: Encoding);
            var doc = new HtmlDocument();
            doc.LoadHtml(showtimesSource.Content);

            var root = doc.DocumentNode;

            var dayNodes = root.SelectNodes("//div[@class='showing']");
            if (dayNodes == null)
            {
                this.GetDefaultLogger().WarnFormat("kinpoisk. No showtimes found {0} {1}", city.Name, url);
                this.GetLogger("KinopoiskLogger").Info(showtimesSource.Content);
                return;
            }
            foreach (var dayNode in dayNodes)
            {
                var model = new MovieScheduleStatsEntities();
                try
                {
                    var dateNode = dayNode.SelectSingleNode("div[@class='showDate']");
                    if (dateNode == null) dateNode = dayNode.SelectSingleNode("div[@class='showDate gray']"); ;
                    if (dateNode == null) continue;
                    var dateRaw = dateNode.TrimDecode();
                    var dateRawArray = dateRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var lastDate = dateRawArray[dateRawArray.Length - 1];
                    DateTime showTimeDate = DateTime.Parse(lastDate, CultureInfo.GetCultureInfo("ru-RU"));
                    var movieNodes = dayNode.SelectNodes("div/div[@class='title _FILM_']");
                    if (movieNodes == null)
                    {
                        return;
                    }

                    int i = 0;
                    foreach (var node in movieNodes)
                    {
                        var movieNode = node.ParentNode;
                        var titleNode = movieNode.SelectSingleNode("div[@class='title _FILM_']/div/p/a");
                        var title = titleNode.TrimDecode();
                        Movie movie;
                        Cinema cinema;


                        if (optionalMovies != null && optionalMovies.Count > 0)
                        {
                            if (!optionalMovies.Contains(title)) continue;
                        }
                        movie = model.Movies.FirstOrDefault(x => x.Title == title);

                        if (movie == null) continue;
                        //if (!movie.Title.Contains("Сойка")) continue;

                        var showTimeNodes = movieNode.SelectNodes("div[@class='showing_section']/dl");
                        foreach (var showTimeNode in showTimeNodes)
                        {
                            var cinemaLink = showTimeNode.SelectSingleNode("dt[@class='name']").ParseLink(new Uri(URLs.Base));
                            cinemaLink.Text = cinemaLink.Text.ReplaceBadChar().Replace("  ", " ");

                            //if (!cinemaLink.Text.Contains("Теплый Стан")) continue;

                            if (ParsingHelper.SkipNotCurrentCityCinema(cinemaLink, model))
                                continue;

                            var replacementCity = ParsingHelper.TryGetReplacementCity(cinemaLink, model);
                            City cityToUse = replacementCity ?? city;
                            var originalCity = city.Name;
                            cinema = ParsingHelper.GetCinema(cinemaLink, cityToUse, TargetSiteShortcut, model, originalCity, GenerateSourceForCinema);

                            //cinema = ParsingHelper.GetCinemaForShowtime(model, cityToUse, cinemaLink, GenerateSourceForCinema);

                            var sessionNodes = showTimeNode.SelectNodes("dd[@class='time']");
                            foreach (var sessionNode in sessionNodes)
                            {
                                var showtime = new Showtime
                                {
                                    SessionsFormat = ParseSessionFormat(sessionNode, movie.Format).ToString(),
                                    TargetSiteId = TargetSiteShortcut.GetTargetSite().Id,
                                    Movie = movie,
                                    MovieId = movie.Id,
                                    Cinema = cinema,
                                    CinemaId = cinema.Id,
                                    Date = showTimeDate.Date,
                                    CreationDate = DateTime.Now,
                                    ParseRunId = ParseRunProvider.Instance.GetParseRun().Id,
                                    CityId = city.Id
                                };

                                var timeNodes = sessionNode.GetNodes("i", "u", "b");

                                ParsingHelper.PopulateSessions(showtime, timeNodes);

                                showtime.SessionsCollection = showtime.SessionsCollection.OrderBy(x => x, DataHelper.SessionsComparer).ToList();

                                showtime.Additional = string.Empty;
                                showtime.URL = string.Empty;
                                var format = showtime.SessionsFormat;
                                showtime = ParsingHelper.CheckShowtimePresent(showtime, model);
                                if (showtime != null)
                                {
                                    model.Showtimes.Add(showtime);
                                }

                                ParsingHelper.TryAddSnapshot(this.TargetSite, model, movie, cinema, showTimeDate, showtime, format.ToString());

                                model.SaveChanges();
                            }
                        }
                        i++;
                    }
                }
                finally
                {
                    model.Dispose();
                }
            }
        }

        private static SessionFormat ParseSessionFormat(HtmlNode sessionNode, string movieFormat = "")
        {
            var hallNode = sessionNode.NextSibling.NextSibling.SelectSingleNode("u");
            if (hallNode == null)
                return SessionFormat.TwoD;
            if (hallNode.InnerText == "3D")
                return movieFormat == "2D" ? SessionFormat.TwoD : SessionFormat.ThreeD;
            return SessionFormat.IMAX;
        }

        private static Source GenerateSourceForCinema(Cinema cinema, City city, Link cinemaLink, string originalCity)
        {

            //var possibleCity = cinemaLink.Text.GetValueInBrackets();
            //int cityId = city.Id;
            //string cinemaName = cinemaLink.Text;
            //if (!string.IsNullOrWhiteSpace(possibleCity))
            //{
            //    using (var model = new MovieScheduleStatsEntities())
            //    {
            //        var repalcementCity = DataHelper.TryGetReplacementCity(possibleCity, model);
            //        if (repalcementCity != null)
            //        {
            //            cinemaName = cinemaName.RemoveValueInBrackets(possibleCity);
            //            cityId = repalcementCity.Id;
            //        }
            //    }
            //}
            var source = new Source
                {
                    Cinema = cinema,
                    CityId = city.Id,
                    TargetSite = TargetSiteShortcut,
                    TargetSiteId = TargetSiteShortcut.GetTargetSite().Id,
                    Parameter = cinemaLink.Reference.ToString(),
                    CreationDate = DateTime.Now,
                    URL = new Uri(new Uri(URLs.Base), cinemaLink.Reference).ToString(),
                    OriginalCity = originalCity,
                    Text = cinemaLink.Text
                };
            return source;
        }

        private Source GenerateSourceForCity(City dbCity, string cityId, string originalCity = "")
        {
            var source = new Source
            {
                City = dbCity,
                Parameter = cityId,
                TargetSite = TargetSiteShortcut,
                TargetSiteId = TargetSiteShortcut.GetTargetSite().Id,
                CreationDate = DateTime.Now,
                URL = new Uri(new Uri(URLs.City), cityId).ToString(),
                OriginalCity = originalCity
            };
            return source;
        }

        #endregion

        #region Old methods

        public void ParseCinemasOld()
        {
            var sw = new Stopwatch();
            sw.Start();
            var cities = new List<City>();
            using (var model = new MovieScheduleStatsEntities())
            {
                cities = model.Cities.Include("Sources").Where(x => x.Sources.Any(xx => xx.TargetSite == TargetSiteShortcut)).ToList();
            }

            var result = new StringBuilder();
            int pagesCount = 0;
            foreach (var city in cities)
            {
                pagesCount++;
                //result.AppendLine(city.Name);
                try
                {
                    var response =
                        HttpSender.GetHtmlResponse(string.Format(URLs.CinemasFormat, city.Sources.First().Parameter), encoding: Encoding);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(response.Content);
                    var root = doc.DocumentNode;

                    var names = root.SelectNodes("//a[@itemprop='name']");


                    foreach (var name in names)
                    {
                        result.AppendLine(string.Format("{0},{1}", city.Name, name.InnerText));
                    }
                    //Thread.Sleep(100);
                }
                catch (Exception)
                {

                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed {0} to parse {1} pages", sw.Elapsed, pagesCount);
            File.WriteAllText(@"D:\cinemas_cities.csv", result.ToString(), Encoding.Unicode);
        }

        private void ParseShowtimesForCityMobile(Source citySource)
        {
            var url = string.Format(URLs.ShowtimesFormat, DateTime.Now);
            var headers = new RequestHeaders();
            headers = Defaults.Headers.Clone();
            //Moscow
            if (citySource.Parameter != "1")
                headers.Cookies.Add(new Cookie("tc", citySource.Parameter) { Domain = ".kinopoisk.ru" });
            var showtimesSource = HttpSender.GetHtmlResponse(url, encoding: Encoding);
            var doc = new HtmlDocument();
            doc.LoadHtml(showtimesSource.Content);
            var root = doc.DocumentNode;

            //root.SelectNodes()
        }

        private void ParseCitiesMobile()
        {
            var citiesSource = HttpSender.GetHtmlResponse(URLs.City, encoding: Encoding);
            var doc = new HtmlDocument();
            doc.LoadHtml(citiesSource.Content);
            var root = doc.DocumentNode;
            var select = root.SelectSingleNode("//select[@name='city']");
            var nodes = select.SelectNodes("option");
            foreach (var node in nodes)
            {
                if (!node.Attributes.Contains("value") || node.Attributes["value"].Value == "+" || string.IsNullOrWhiteSpace(node.NextSibling.InnerText)) continue;

                var cityId = node.Attributes["value"].Value;
                var cityName = NodeHelper.TrimDecode(node.NextSibling.InnerText);
                using (var model = new MovieScheduleStatsEntities())
                {
                    var dbCity = model.Cities.Include("Sources").FirstOrDefault(x => x.Name == cityName);
                    if (dbCity != null)
                    {
                        if (!dbCity.Sources.Any(x => x.TargetSite == TargetSiteShortcut && x.Parameter.Contains(cityId)))
                        {
                            var source = GenerateSourceForCity(dbCity, cityId);
                            dbCity.Sources.Add(source);
                        }
                    }
                    else
                    {
                        var country = ParsingHelper.GetCountry(model);
                        var city = new City
                        {
                            Name = cityName,
                            Country = country
                        };
                        var source = GenerateSourceForCity(city, cityId);
                        city.Sources.Add(source);
                        model.Cities.Add(city);
                    }
                    model.SaveChanges();
                }
            }
        }

        #endregion
    }
}
