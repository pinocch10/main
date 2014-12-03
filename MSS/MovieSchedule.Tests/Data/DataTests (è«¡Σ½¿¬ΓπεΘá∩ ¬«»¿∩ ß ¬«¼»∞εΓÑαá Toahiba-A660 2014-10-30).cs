using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Excel;
using log4net.Appender;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Web.Security;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace MovieSchedule.Tests.Data
{
    [TestClass]
    public class DataTests
    {
        private const string MasterListSource = @"C:\master-list-source.txt";

        [TestMethod]
        public void DeviationTest()
        {
            int movieId = 60;
            int cinemaId = 13;
            DateTime date = new DateTime(2014, 10, 30);
            using (var model = new MovieScheduleStatsEntities())
            {
                var snapshots = (from x in model.ShowtimeSnapshots
                                 where
                                 x.MovieId == movieId
                                 && x.Date == date
                                 && x.CinemaId == cinemaId
                                 orderby x.Date descending
                                 select x).ToList();

                Console.WriteLine(snapshots.Count);
            }

        }

        [TestMethod]
        public void CompareDates()
        {
            var date = new DateTime(2014, 10, 22);
            var difference = DateTime.Now.Subtract(date).Days;
            Assert.AreEqual(7, difference);
        }

        [TestMethod]
        public void CompareStrings()
        {
            string source = "ЗИЛЬС МАРИЯ";
            string toCompare = "годзилла";

            int result = LevenshteinDistance.Compute(source.ToLower(), toCompare.ToLower());
            Console.WriteLine("{0} ({1}) - {2} ({3}) -> {4}", source, source.Length, toCompare, toCompare.Length, result);
        }

        [TestMethod]
        public void GenerateMD5Hash()
        {
            var password = "7RuItA0QaR";
            var passwordHash = EncryptingExtensions.GetMD5Hash(password);
            Console.WriteLine("{0} -> {1}", password, passwordHash);
        }

        [TestMethod]
        public void RegexTest()
        {
            var tt = "Костино (Королёв)".ReplaceBadChar();
            var t = Regex.Replace("Киноград (Лесной городок)", string.Format("\\({0}\\)", "Лесной Городок"), string.Empty, RegexOptions.IgnoreCase);
            Console.WriteLine(t);
        }

        [TestMethod]
        public void CitySatelites()
        {
            using (var model = new MovieScheduleStatsEntities())
            {

                var country = ParsingHelper.GetCountry(model);
                var masterCity = new City
                    {
                        Name = "Санкт-Петербург",
                        CountryId = country.Id,
                    };
                model.Cities.Add(masterCity);
                var sateliteCity1 = new City
                {
                    Name = "Колпино",
                    CountryId = country.Id,
                    SatelliteTo = masterCity
                };
                model.Cities.Add(sateliteCity1);
                var sateliteCity2 = new City
                {
                    Name = "Пулково",
                    CountryId = country.Id,
                    SatelliteTo = masterCity
                };
                model.Cities.Add(sateliteCity2);
                model.SaveChanges();
                masterCity = model.Cities.FirstOrDefault(x => x.Name == "Санкт-Петербург");
                Assert.IsNotNull(masterCity, "Master city should be present");
                Assert.AreEqual(2, masterCity.Satellites.Count, "There should be 2 satellites");
            }
        }

        [TestMethod]
        public void Showtimes()
        {
            var uri = new Uri("http://www.afisha.ru/abakan/changecity/");
            Console.WriteLine(uri.AbsolutePath);

            var cinema = new Cinema
            {
                Name = "Name",
                CityId = 1,
                CreationDate = DateTime.Now,
            };

            var showtimeKinopoisk = new Showtime
            {
                MovieId = 1,
                CinemaId = 2,
                Sessions = "10:00; 11:00; 12:00; 13:00",
                SessionsCount = 4,
                SessionsFormat = SessionFormat.TwoD.ToString(),
                TargetSite = "kinopoisk.ru".GetTargetSite(),
                Date = DateTime.Today,
                CreationDate = DateTime.Now
            };

            var showtimeAfisha = new Showtime
            {
                MovieId = 1,
                CinemaId = 2,
                Sessions = "10:00; 11:00",
                SessionsCount = 2,
                SessionsFormat = SessionFormat.FourDX.ToString(),
                TargetSite = "afisha.ru".GetTargetSite(),
                Date = DateTime.Today,
                CreationDate = DateTime.Now
            };
            var showtimeKinopoisk2 = new Showtime
            {
                MovieId = 1,
                CinemaId = 2,
                Sessions = "10:00; 11:00",
                SessionsCount = 2,
                SessionsFormat = SessionFormat.TwoD.ToString(),
                TargetSite = "kinopoisk.ru".GetTargetSite(),
                Date = DateTime.Today,
                CreationDate = DateTime.Now
            };
            using (var model = new MovieScheduleStatsEntities())
            {
                //model.Cinemas.Add(cinema);
                model.SaveChanges();
                model.Showtimes.Add(showtimeKinopoisk);
                model.SaveChanges();
                showtimeAfisha = ParsingHelper.CheckShowtimePresent(showtimeAfisha, model);
                if (showtimeAfisha != null)
                    model.Showtimes.Add(showtimeAfisha);
                model.SaveChanges();
                showtimeKinopoisk2 = ParsingHelper.CheckShowtimePresent(showtimeKinopoisk2, model);
                if (showtimeKinopoisk2 != null)
                    model.Showtimes.Add(showtimeKinopoisk2);
                model.SaveChanges();

            }
        }

        private Dictionary<string, List<string>> GetDict()
        {
            var source = new Dictionary<string, List<string>>();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();
                            if (source.Keys.Contains(city))
                                source[city].Add(cinema);
                            else
                                source.Add(city, new List<string> { cinema });
                        }
                    }
                }
            }
            return source;
        }

        [TestMethod]
        public void CheckExistingCinemas_Exclusion()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                //var cinemas = model.Cinemas.Where(x => x.City.Name == city && x.Name == cinema &&
                                //                                        x.Sources.Any(xx => xx.TargetSiteShortcut == "afisha.ru"))
                                //                    .ToList();
                                //&& x.Sources.Any(xx => xx.TargetSiteShortcut == "kinopoisk.ru")))

                                var values = source[city];

                                var cinemas = model.Cinemas.Where(x => x.City.Name == city && !values.Contains(x.Name)
                                    && x.Sources.Any(xx => xx.TargetSite == "afisha.ru")
                                    //&& x.Sources.Any(xx => xx.TargetSiteShortcut == "kinopoisk.ru")
                                    ).Select(x => x.Name)
                                .ToList();

                                string ccc = string.Empty;
                                if (cinemas.Count > 0)
                                {
                                    if (lastCity != city)
                                    {
                                        lastCity = city;
                                        ccc = string.Join("; ", cinemas);
                                    }
                                }
                                if (string.IsNullOrEmpty(ccc))
                                    sb.AppendFormat(
                                        "<tr><td style='color:#BDBDBD'>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                        city, ccc);
                                else
                                {
                                    sb.AppendFormat("<tr><td>{0}</td><td>{1}</td></tr>\r\n", city, ccc);
                                }

                                //sb.AppendFormat("<tr><td><b>{0}</b></td><td><b>{1}</b></td></tr>\r\n", items[0], items[1]);

                                //else
                                //{
                                //    sb.AppendFormat("<tr><td style='color:#BDBDBD'>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n", items[0], items[1]);
                                //}
                            }
                        }
                    }
                }
            }

            //using (var model = new MovieScheduleStatsEntities())
            //{
            //    foreach (var s in source)
            //    {
            //        var cinemas = model.Cinemas.Where(x => x.City.Name == s.Key && !s.Value.Contains(x.Name)
            //            //&& x.Sources.All(xx => xx.TargetSiteShortcut == "afisha.ru")))
            //            //&& x.Sources.All(xx => xx.TargetSiteShortcut == "kinopoisk.ru")))

            //                                            //&& x.Sources.Any(xx => xx.TargetSiteShortcut == "afisha.ru")
            //            //&& x.Sources.Any(xx => xx.TargetSiteShortcut == "kinopoisk.ru")))

            //                                            &&
            //                                            x.Sources.Any(xx => xx.TargetSiteShortcut == "afisha.ru")).Select(x => x.Name)
            //                        .ToList();
            //        //&& x.Sources.Any(xx => xx.TargetSiteShortcut == "kinopoisk.ru")))
            //        if (cinemas.Count > 0)
            //        {
            //            sb.AppendFormat("<tr><td><b>{0}</b></td><td><b>{1}</b></td></tr>\r\n", s.Key, string.Join("; ", cinemas));
            //        }
            //        else
            //        {
            //            sb.AppendFormat("<tr><td style='color:#BDBDBD'>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n", items[0], items[1]);
            //        }
            //    }

            //}
            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result.txt", sb.ToString());
        }

        [TestMethod]
        public void CheckExistingCinemas_Inclusion_All()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                var cinemaFound = model.Cinemas.Any(x => x.City.Name == city && x.Name == cinema
                                                                     &&
                                                                     x.Sources.Any(xx => xx.TargetSite == "afisha.ru")
                                                                     &&
                                                                     x.Sources.Any(xx => xx.TargetSite == "kinopoisk.ru"));


                                if (cinemaFound)
                                {
                                    sb.AppendFormat(
                                        "<tr><td>{0}</td><td><b>{1}</b></td></tr>\r\n",
                                        city, cinema);
                                }
                                else
                                {
                                    sb.AppendFormat(
                                            "<tr><td>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                            city, cinema);
                                }

                            }
                        }
                    }
                }
            }

            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result_inclusion_all.txt", sb.ToString());
        }

        [TestMethod]
        public void CheckExistingCinemas_Inclusion_KP()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                var cinemaFound = model.Cinemas.Any(x => x.City.Name == city && x.Name == cinema
                                                                     &&
                                                                     x.Sources.All(xx => xx.TargetSite == "kinopoisk.ru"));


                                if (cinemaFound)
                                {
                                    sb.AppendFormat(
                                        "<tr><td>{0}</td><td><b>{1}</b></td></tr>\r\n",
                                        city, cinema);
                                }
                                else
                                {
                                    sb.AppendFormat(
                                            "<tr><td>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                            city, cinema);
                                }

                            }
                        }
                    }
                }
            }

            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result_inclusion_kinopoisk.txt", sb.ToString());
        }

        [TestMethod]
        public void CheckExistingCinemas_Inclusion_AF()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                var cinemaFound = model.Cinemas.Any(x => x.City.Name == city && x.Name == cinema
                                                                     &&
                                                                     x.Sources.All(xx => xx.TargetSite == "afisha.ru"));


                                if (cinemaFound)
                                {
                                    sb.AppendFormat(
                                        "<tr><td>{0}</td><td><b>{1}</b></td></tr>\r\n",
                                        city, cinema);
                                }
                                else
                                {
                                    sb.AppendFormat(
                                            "<tr><td>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                            city, cinema);
                                }

                            }
                        }
                    }
                }
            }

            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result_inclusion_afisha.txt", sb.ToString());
        }


        private string _masterListFile = @"c:\Users\mpak\Dropbox\MovieSchedule\Documents\master-list+kp+afisha-updated.xlsx";
        //private string _masterListFile = @"c:\Users\mikachi\Dropbox\MovieSchedule\Documents\master-list+kp+afisha-updated.xlsx";

        [TestMethod]
        public void ParseMasterListEPP()
        {
            Dictionary<string, int> cinemas = DataHelper.ParseMasterList(_masterListFile);

            foreach (var cinema in cinemas.Where(x => x.Value > 1))
            {
                Console.WriteLine("{0} - {1}", cinema.Key, cinema.Value);
            }
        }

        [TestMethod]
        public void ParseMasterListExcel()
        {
            var sheets = Workbook.Worksheets(_masterListFile);

            foreach (var worksheet in sheets)
            {
                foreach (var row in worksheet.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        var t = cell.Text;
                    }
                }
            }
        }

        [TestMethod]
        public void LoadCityTest()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var city = model.Cities.FirstOrDefault(x => x.Name == "Ахтубинск");
                Assert.IsNotNull(city);
                Assert.IsNotNull(city.Sources);
            }
        }

        class CityFederalDistrict
        {
            internal string City { get; set; }
            internal string FederalDistrict { get; set; }
        }

        [TestMethod]
        public void LoadFederalDistrictsTest()
        {
            var csvPath = @"C:/Users/mikachi/Desktop/fo_cities.csv";
            var federalDistrictCitites = new Dictionary<string, List<string>>();
            //var citiesFederalDistrict = new Dictionary<string, string>();
            var citiesFederalDistrict = new List<CityFederalDistrict>();
            using (var fs = new FileStream(csvPath, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (sr.Peek() > 0)
                    {
                        var line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            Console.WriteLine("Empty line");
                            continue;
                        }
                        var array = line.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (array.Length == 0 || array.Length != 3)
                        {
                            Console.WriteLine(line);
                            Console.WriteLine("Inconsistent array");
                            continue;
                        }
                        var city = array[0];
                        var federalDistrict = array[2];
                        federalDistrictCitites.AddOrUpdate(federalDistrict, new List<string> { city });
                        citiesFederalDistrict.Add(new CityFederalDistrict { City = city.Replace("ё", "е").Replace("Ё", "Е"), FederalDistrict = federalDistrict });
                    }
                    int foundCounts = 0;
                    using (var model = new MovieScheduleStatsEntities())
                    {
                        var federalDistricts = new List<FederalDistrict>();
                        foreach (var kv in federalDistrictCitites.Keys)
                        {
                            var fd = new FederalDistrict { Name = kv };
                            federalDistricts.Add(fd);
                            model.FederalDistricts.Add(fd);
                        }
                        model.SaveChanges();

                        int count = 0;
                        foreach (var cfd in citiesFederalDistrict)
                        {
                            var storedCity = model.Cities.FirstOrDefault(xx => xx.Name == cfd.City);
                            if (storedCity != null)
                            {
                                Console.WriteLine("UPDATE [dbo].[City] SET [FederalDistrictId] = {0} WHERE Id = {1}", federalDistricts.Find(x => x.Name == cfd.FederalDistrict).Id, storedCity.Id);
                                count++;
                            }
                        }
                        foundCounts += count;
                    }
                    Console.WriteLine("{0} cities found, {1} cities matched", citiesFederalDistrict.Count, foundCounts);
                }
            }
        }

        [TestMethod]
        public void CheckVolgaFilms()
        {
            string masterListFile = @"D:/Hunger_Games_Part1.xlsx";
            using (ExcelPackage pck = new ExcelPackage(new FileInfo(masterListFile)))
            {
                using (var model = new MovieScheduleStatsEntities())
                {

                    int cityIndex = 4;
                    int cinemaIndex = 5;
                    int foundCount = 0;
                    int nonStrictMatchCount = 0;
                    foreach (var worksheet in pck.Workbook.Worksheets)
                    {
                        int columnsCount = worksheet.Dimension.End.Column + 1;
                        int rowsCount = worksheet.Dimension.End.Row + 1;

                        for (int i = 3; i < rowsCount; i++)
                        {

                            string city = GetCellValue(worksheet, i, cityIndex);
                            string cinema = GetCellValue(worksheet, i, cinemaIndex);

                            if (
                                model.Cinemas.Any(x => x.Name == cinema &&
                                    (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)))
                                || model.Cinemas.Any(x => (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Sources.Any(xx => xx.Text == cinema))
                                )
                            {
                                foundCount++;
                                worksheet.Cells[i, cityIndex].Style.Font.Bold = true;
                                worksheet.Cells[i, cinemaIndex].Style.Font.Bold = true;
                                worksheet.Row(i).Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheet.Row(i).Style.Fill.PatternColor.SetColor(Color.Yellow);
                                worksheet.Row(i).Style.Fill.BackgroundColor.SetColor(Color.Yellow);
                            }
                            else
                            {
                                var cnm =
                                    model.Cinemas.FirstOrDefault(x => x.Name.Contains(cinema) && (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city))) ??
                                    model.Cinemas.FirstOrDefault(x => (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Sources.Any(xx => xx.Text.Contains(cinema)));
                                if (cnm == null)
                                {
                                    cnm = model.Cinemas.FirstOrDefault(x => cinema.Contains(x.Name) && (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city))) ??
                                    model.Cinemas.FirstOrDefault(x => (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Sources.Any(xx => cinema.Contains(xx.Text)));
                                    if (cnm != null)
                                        Console.WriteLine("Reverse search");
                                }

                                if (cnm != null)
                                {
                                    nonStrictMatchCount++;
                                    Console.WriteLine("{0} | {1} | {2}", city, cinema, cnm.Name);
                                    foundCount++;
                                    worksheet.Cells[i, cityIndex].Style.Font.Bold = true;
                                    worksheet.Cells[i, cinemaIndex].Style.Font.Bold = true;
                                    worksheet.Row(i).Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    worksheet.Row(i).Style.Fill.PatternColor.SetColor(Color.Yellow);
                                    worksheet.Row(i).Style.Fill.BackgroundColor.SetColor(Color.Yellow);
                                }
                            }
                        }
                    }
                    pck.Save();
                    Console.WriteLine("{0} ({1})", foundCount, nonStrictMatchCount);
                }
            }
        }

        private static string GetCellValue(ExcelWorksheet worksheet, int i, int j)
        {
            var cell = worksheet.Cells[i, j];

            return cell.Text.Replace("\"\"\"", string.Empty)
                .Replace("ё", "е").Replace("Ё", "Е")
                .Trim(' ', ';');
        }
    }
}
