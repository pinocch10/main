using System;
using System.Collections.Generic;
using MovieSchedule.Data;

namespace MovieSchedule.Web.Models
{
    public class GrouppedShowtime
    {
        public string Format { get; set; }
        public int MovieId { get; set; }
        public string MovieTitle { get; set; }
        public int CountryId { get; set; }
        public string CountryName { get; set; }
        public int CinemaId { get; set; }
        public string CinemaName { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; }
        public DateTime Date { get; set; }
        public List<Showtime> Showtimes { get; set; }
    }

    public class CompareModel
    {

        private List<MovieModel> _allMovies = new List<MovieModel>();
        private List<DateTime> _dates = new List<DateTime>();

        public DateTime Date { get; set; }

        public List<DateTime> Dates
        {
            get { return _dates; }
            set { _dates = value; }
        }

        public List<MovieModel> AllMovies
        {
            get { return _allMovies; }
            set { _allMovies = value; }
        }
    }
}