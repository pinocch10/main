using System.Collections.Generic;
using System.Text;
using MovieSchedule.Data;

namespace MovieSchedule.Parsers.Common
{
    public interface IMovieScheduleParser
    {
        Encoding Encoding { get; }
        TargetSite TargetSite { get; }

        string GetTargetSite();
        string GetBaseCityUrl();

        Dictionary<string, List<Link>> ParseCinemas();
        void ParseShowtimes();
        void ParseCities();
        void Parse();
    }
}