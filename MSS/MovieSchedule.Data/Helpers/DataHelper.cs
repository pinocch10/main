using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MovieSchedule.Core.Extensions;
using OfficeOpenXml;

namespace MovieSchedule.Data.Helpers
{
    public class Constants
    {
        public static readonly string Kinopoisk = "kinopoisk.ru";
        public static readonly string Afisha = "afisha.ru";

        public class Tables
        {
            public const string Movie = "Movie";
            public const string Cinema = "Cinema";
        }

        public class Columns
        {
            public const string Title = "Title";
            public const string Name = "Name";
        }
    }

    public class MovieSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public DateTime ReleaseDate { get; set; }
        public double Simmilarity { get; set; }
    }

    public class SearchResult
    {
        public string Table { get; set; }
        public string Column { get; set; }
        public double Deviation { get; set; }
        public string SearchTerm { get; set; }
        public List<Suggestion> Suggestions { get; set; }
    }

    public class Suggestion
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public double Simmilarity { get; set; }
    }


    public static class DataHelper
    {
        private static bool _initialized;
        private static string _deviationQueryWithPlaceholders = "SELECT [Id], [%column%] AS [Value], dbo.fn_LevenshteinDistancePercentage(REPLACE([%column%],' ',''),REPLACE(ISNULL(@searchTerm,''),' ','')) AS Simmilarity FROM [dbo].[%table%] WHERE dbo.fn_LevenshteinDistancePercentage(REPLACE([%column%],' ',''),REPLACE(ISNULL(@searchTerm,''),' ','')) > %suggestDeviation%";

        private static string BuildSuggestionQuery(string table, string column, double suggestDeviation)
        {
            var sb = new StringBuilder(_deviationQueryWithPlaceholders);

            sb = sb.Replace("%suggestDeviation%", suggestDeviation.ToString(CultureInfo.InvariantCulture));
            sb = sb.Replace("%table%", table);
            sb = sb.Replace("%column%", column);

            return sb.ToString();
        }


        public static SearchResult GetCinemaSuggestions(string searchTerm, double suggestDeviation = 85)
        {
            return GetSuggestions(
             table: Constants.Tables.Cinema,
             column: Constants.Columns.Name,
             searchTerm: searchTerm,
             suggestDeviation: suggestDeviation);
        }

        public static SearchResult GetMovieSuggestions(string searchTerm, double suggestDeviation = 85)
        {
            return GetSuggestions(
             table: Constants.Tables.Movie,
             column: Constants.Columns.Title,
             searchTerm: searchTerm,
             suggestDeviation: suggestDeviation);
        }

        public static SearchResult GetSuggestions(
            string table,
            string column,
            string searchTerm,
            double suggestDeviation = 85)
        {
            var result = new SearchResult
            {
                Column = column,
                Table = table,
                Deviation = suggestDeviation,
                SearchTerm = searchTerm
            };

            using (var model = new MovieScheduleStatsEntities())
            {
                result.Suggestions =
                    model.Database.SqlQuery<Suggestion>(BuildSuggestionQuery(table, column, suggestDeviation),
                        new SqlParameter("searchTerm", searchTerm)).ToList();
            }

            return result;
        }

        public static City TryGetReplacementCity(string cityName, MovieScheduleStatsEntities model)
        {
            return model.Cities.FirstOrDefault(x => x.Name == cityName);
        }

        static DataHelper()
        {
            InitTargetSites();
        }

        #region TargetSite
        private static readonly List<TargetSite> TargetSitesCache = new List<TargetSite>();
        private static Comparer<string> _sessionsComparer = null;

        public static TargetSite GetTargetSite(this string shortcut)
        {
            var result = TargetSitesCache.FirstOrDefault(x => x.Shortcut == shortcut);
            if (result != null) return result;
            using (var model = new MovieScheduleStatsEntities())
            {
                result = model.TargetSites.FirstOrDefault(x => x.Shortcut == shortcut);
                TargetSitesCache.Add(result);
                return result;
            }
        }

        public static void InitTargetSites()
        {
            if (_initialized) return;
            using (var model = new MovieScheduleStatsEntities())
            {
                if (model.TargetSites.Any()) return;
                model.TargetSites.Add(new TargetSite { Shortcut = Constants.Kinopoisk });
                model.TargetSites.Add(new TargetSite { Shortcut = Constants.Afisha });
                model.SaveChanges();
                _initialized = true;
            }
        }
        #endregion

        public static Dictionary<string, int> ParseMasterList(string masterListFile)
        {
            InitTargetSites();
            Dictionary<string, int> cinemas = new Dictionary<string, int>();

            using (ExcelPackage pck = new ExcelPackage(new FileInfo(masterListFile)))
            {
                using (var model = new MovieScheduleStatsEntities())
                {
                    foreach (var worksheet in pck.Workbook.Worksheets)
                    {
                        int cols = worksheet.Dimension.End.Column + 1;
                        int rows = worksheet.Dimension.End.Row + 1;

                        bool currentCityChanged = false;
                        var sb = new StringBuilder();
                        for (int i = 2; i < rows; i++)
                        {

                            var row = ParseMasterListRow(i, cols, worksheet, model, cinemas);
                            sb.AppendLine(row);
                        }
                        File.WriteAllText(@"C:/master-list.csv", sb.ToString(), Encoding.UTF8);
                        //Only first worksheet needed
                        break;
                    }
                }
            }
            return cinemas;
        }

        private static string ParseMasterListRow(int rowIndex, int columnsCount, ExcelWorksheet worksheet, MovieScheduleStatsEntities model, Dictionary<string, int> cinemas)
        {
            Cinema currentCinema = null;
            City currentCity = null;

            var sb = new StringBuilder();
            const int cityCol = 1;
            const int bothCol = 2;
            const int kpCol = 3;
            const int kpNewCol = 4;
            const int afishaCol = 5;
            const int afishaNewCol = 6;

            bool requireSave = false;
            bool afishaFound = false;
            bool kinopoiskFound = false;
            for (int j = 1; j < columnsCount; j++)
            {
                var cell = worksheet.Cells[rowIndex, j];
                var text = cell.Text.Replace("\"\"\"", String.Empty)
                    //.Replace("ё", "е").Replace("Ё", "Е")
                    .Trim(' ', ';');

                switch (j)
                {
                    case cityCol:
                        currentCity = TryGenerateCity(text, model);
                        sb.AppendFormat("{0}|", text);
                        break;
                    case bothCol:
                        currentCinema = TryGenerateCinema(text, currentCity, model);
                        sb.AppendFormat("{0}|", text);
                        //Both sites have it
                        if (cell.Style.Font.Bold)
                        {
                            sb.AppendFormat("{0};{1}", text, text);
                            requireSave = GenerateSource(Constants.Afisha, currentCity, currentCinema, text, model) || requireSave;
                            requireSave = GenerateSource(Constants.Kinopoisk, currentCity, currentCinema, text, model) || requireSave;
                            afishaFound = true;
                            kinopoiskFound = true;
                        }
                        break;
                    case kpCol:
                        if (!kinopoiskFound && cell.Style.Font.Bold)
                        {
                            sb.AppendFormat("{0}|", text);
                            kinopoiskFound = GenerateSource(Constants.Kinopoisk, currentCity, currentCinema, text, model);
                            requireSave = kinopoiskFound || requireSave;
                        }
                        break;
                    case kpNewCol:
                        if (!kinopoiskFound && !String.IsNullOrWhiteSpace(text))
                        {
                            if (text.Contains(";"))
                            {
                                var texts = text.Split(';');
                                foreach (var t in texts)
                                {
                                    kinopoiskFound = AddNew(model, cell, kinopoiskFound, currentCity, currentCinema, t, ref requireSave, Constants.Kinopoisk);
                                }
                            }
                            else
                            {
                                kinopoiskFound = AddNew(model, cell, kinopoiskFound, currentCity, currentCinema, text, ref requireSave, Constants.Kinopoisk);
                            }
                            sb.AppendFormat("{0}|", text);
                        }
                        //Yellow marker
                        if (cell.Style.Fill.BackgroundColor.Rgb == "FFFFFF00")
                        {
                            sb.AppendFormat("{0}|", text);
                            var cinema = TryGenerateCinema(text, currentCity, model, false);
                            requireSave = GenerateSource(Constants.Kinopoisk, currentCity, cinema, text, model) || requireSave;
                        }
                        break;
                    case afishaCol:
                        if (!afishaFound && cell.Style.Font.Bold)
                        {
                            sb.AppendFormat("{0}|", text);
                            afishaFound = GenerateSource(Constants.Afisha, currentCity, currentCinema, text, model);
                            requireSave = afishaFound || requireSave;
                        }
                        break;
                    case afishaNewCol:
                        if (!afishaFound && !String.IsNullOrWhiteSpace(text))
                        {
                            if (text.Contains(";"))
                            {
                                var texts = text.Split(';');
                                foreach (var t in texts)
                                {
                                    kinopoiskFound = AddNew(model, cell, kinopoiskFound, currentCity, currentCinema, t, ref requireSave, Constants.Afisha);
                                }
                            }
                            else
                            {
                                kinopoiskFound = AddNew(model, cell, kinopoiskFound, currentCity, currentCinema, text, ref requireSave, Constants.Afisha);
                            }
                            sb.AppendFormat("{0}|", text);
                        }
                        #region Non-master-list cinemas
                        //Yellow marker
                        if (cell.Style.Fill.BackgroundColor.Rgb == "FFFFFF00")
                        {
                            var cinema = TryGenerateCinema(text, currentCity, model, false);
                            requireSave = GenerateSource(Constants.Afisha, currentCity, cinema, text, model) || requireSave;
                        }
                        #endregion
                        break;
                }
            }
            if (requireSave)
                model.SaveChanges();
            return sb.ToString().Trim('|');
        }

        private static bool AddNew(MovieScheduleStatsEntities model, ExcelRange cell, bool kinopoiskFound, City currentCity,
                                   Cinema currentCinema, string text, ref bool requireSave, string targetSite)
        {
            if (cell.Style.Fill.BackgroundColor.Rgb != "FFFFFF00")
            {
                kinopoiskFound = GenerateSource(targetSite, currentCity, currentCinema, text, model);
                requireSave = kinopoiskFound || requireSave;
            }
            return kinopoiskFound;
        }

        private static bool GenerateSource(string targetSite, City currentCity, Cinema currentCinema, string cinemaName, MovieScheduleStatsEntities model)
        {
            if (!model.Sources.Any(x => x.TargetSite == targetSite && x.CityId == currentCity.Id && x.CinemaId == currentCinema.Id))
            {
                var possibleCity = cinemaName.GetValueInBrackets();
                City repalcementCity = null;
                cinemaName = GetReplacementCity(cinemaName, currentCity, model, possibleCity, ref repalcementCity);
                var source = new Source
                    {
                        TargetSite = targetSite,
                        TargetSiteId = targetSite.GetTargetSite().Id,
                        //CityId = currentCity.Id,
                        City = currentCity,
                        //CinemaId = currentCinema.Id,
                        Cinema = currentCinema,
                        Parameter = String.Empty,
                        URL = String.Empty,
                        Text = cinemaName,
                        CreationDate = DateTime.Now
                    };
                model.Sources.Add(source);
                return true;
            }

            return false;
        }

        private static City TryGenerateCity(string name, MovieScheduleStatsEntities model, int countryId = 1)
        {
            var result = model.Cities.FirstOrDefault(x => x.Name == name);
            if (result != null) return result;
            result = new City { Name = name, CountryId = countryId };
            model.Cities.Add(result);
            model.SaveChanges();
            return result;
        }

        private static Cinema TryGenerateCinema(string name, City city, MovieScheduleStatsEntities model, bool masterListBased = true)
        {
            var result = model.Cinemas.FirstOrDefault(x => x.Name == name && x.CityId == city.Id);
            if (result != null) return result;
            var possibleCity = name.GetValueInBrackets();
            City replacementCity = null;
            name = GetReplacementCity(name, city, model, possibleCity, ref replacementCity);
            result = new Cinema
                    {
                        Name = name,
                        City = replacementCity ?? city,
                        CreationDate = DateTime.Now,
                        MasterListBased = masterListBased
                    };

            model.Cinemas.Add(result);
            model.SaveChanges();
            return result;
        }

        private static string GetReplacementCity(string name, City city, MovieScheduleStatsEntities model, string possibleCity,
                                                 ref City replacementCity)
        {
            if (!String.IsNullOrWhiteSpace(possibleCity))
            {
                //It's russian and english... Crazy, I know.
                if (possibleCity.Contains("ex ") || possibleCity.Contains("ех "))
                {
                    name = name.Replace(String.Format("({0})", possibleCity), String.Empty).Trim();
                }
                else
                {
                    replacementCity = TryGetReplacementCity(possibleCity, model);
                    if (replacementCity != null)
                    {
                        if (city.Id != replacementCity.Id)
                        {
                            if (replacementCity.SatelliteTo == null)
                                replacementCity.SatelliteTo = city;
                        }
                        name = name.Replace(String.Format("({0})", possibleCity), String.Empty).Trim();
                    }
                }
            }
            return name;
        }

        public static void CleanupData()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                //var
                //model.CleanShowtimes()
            }
        }

        public static Comparer<string> SessionsComparer
        {
            get
            {
                return _sessionsComparer ?? (_sessionsComparer = Comparer<string>.Create((left, right) =>
                {
                    int compare = Comparer<string>.Default.Compare(left, right);
                    if (right.IsLateSession())
                    {
                        int i = left.IsLateSession() ? compare : -1;
                        return i;
                    }
                    if (left.IsLateSession())
                    {
                        int i = right.IsLateSession() ? compare : 1;
                        return i;
                    }
                    return compare;
                }));
            }
        }
    }
}
