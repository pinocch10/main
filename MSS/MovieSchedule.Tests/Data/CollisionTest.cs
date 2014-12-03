using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MovieSchedule.Tests.Data
{
    [TestClass]
    public class CollisionTest
    {
        [TestMethod]
        public void CollisionTestCinema()
        {
            var logFile = File.ReadAllText(
                @"c:\Users\mpak\Dropbox\MovieSchedule\MovieSchedule.Runner\bin\Debug\log\internal\2014-10-10_missing_cinemas.log ");
            using (var fs = new FileStream(@"C:\possible_collision.txt", FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    var city = sr.ReadLine();
                    if (city != null && logFile.Contains(city))
                        Console.WriteLine(city);
                }
            }
        }
    }
}
