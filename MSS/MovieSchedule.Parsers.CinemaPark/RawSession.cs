using System;
using System.Collections.Generic;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Parsers.CinemaPark
{
    public class RawSession
    {
        private List<string> _sessions = new List<string>();
        public Link City { get; set; }
        public Link Cinema { get; set; }
        public Link Movie { get; set; }
        public DateTime Date { get; set; }
        public SessionFormat SessionFormat { get; set; }

        public List<string> Sessions
        {
            get { return _sessions; }
            set { _sessions = value; }
        }

        public RawSession Clone()
        {
            var clone = new RawSession()
            {
                City = this.City.Clone(),
                Cinema = this.Cinema.Clone(),
                Movie = this.Movie.Clone(),
                Date = this.Date,
                SessionFormat = this.SessionFormat
            };

            foreach (var session in Sessions)
            {
                clone.Sessions.Add(session);
            }

            return clone;
        }
    }
}