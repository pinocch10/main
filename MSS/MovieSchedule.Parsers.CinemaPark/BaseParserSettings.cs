using System;
using System.Collections.Generic;
using System.Text;

namespace MovieSchedule.Parsers.CinemaPark
{

    public abstract class BaseParserSettings
    {
        public abstract string Base { get; }
        public abstract string TargetSiteShortcut { get; }
        public abstract string XPathCities { get; }
        public abstract string XPathCinemas { get; }

        public abstract Encoding Encoding { get; }
        public abstract Uri BaseUri { get; }

        public abstract List<DateTime> Dates { get; }
    }
}