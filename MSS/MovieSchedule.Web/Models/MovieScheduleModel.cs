using System;
using System.Collections.Generic;
using MovieSchedule.Data;

namespace MovieSchedule.Web.Models
{

    public class ComparisonShowtime
    {
        public ShowtimeModel LeftShowtime { get; set; }
        public ShowtimeModel RightShowtime { get; set; }
    }

    public class ComparisonModel
    {
        private List<ComparisonShowtime> _showtimes = new List<ComparisonShowtime>();

        public List<ComparisonShowtime> Showtimes
        {
            get { return _showtimes; }
            set { _showtimes = value; }
        }

        public MovieScheduleModel Right { get; set; }
        public MovieScheduleModel Left { get; set; }
    }

    public class MovieScheduleModel : IDisposable
    {
        private List<ShowtimeModel> _showtimes = new List<ShowtimeModel>();
        private List<FederalDistrict> _federalDistricts = new List<FederalDistrict>();
        private List<City> _cities = new List<City>();
        private List<CinemaModel> _cinemas = new List<CinemaModel>();
        private List<CinemaNetwork> _cinemaNetworks = new List<CinemaNetwork>();
        private List<Country> _countries = new List<Country>();
        private List<CinemaModel> _offCinemas = new List<CinemaModel>();

        public string DistributorName { get; set; }
        public string MovieTitle { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public string MovieFormat { get; set; }
        public DateTime MovieReleaseDate { get; set; }
        public DateTime Date { get; set; }

        public FederalDistrict SelectedFederalDistrict { get; set; }
        public City SelectedCity { get; set; }
        public Cinema SelectedCinema { get; set; }
        public CinemaNetwork SelectedCinemaNetwork { get; set; }


        public List<ShowtimeModel> Showtimes
        {
            get { return _showtimes; }
            set { _showtimes = value; }
        }

        public List<FederalDistrict> FederalDistricts
        {
            get { return _federalDistricts; }
            set { _federalDistricts = value; }
        }

        public List<City> Cities
        {
            get { return _cities; }
            set { _cities = value; }
        }

        public List<CinemaModel> Cinemas
        {
            get { return _cinemas; }
            set { _cinemas = value; }
        }

        public List<CinemaNetwork> CinemaNetworks
        {
            get { return _cinemaNetworks; }
            set { _cinemaNetworks = value; }
        }

        public List<Country> Countries
        {
            get { return _countries; }
            set { _countries = value; }
        }

        public List<CinemaModel> OffCinemas
        {
            get { return _offCinemas; }
            set { _offCinemas = value; }
        }

        public int ShowtimesCount { get; set; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class CinemaModel
    {
        public int Id { get; set; }
        public int CityId { get; set; }
        public string DisplayName { get; set; }
        public string CinemaName { get; set; }
        public string CityName { get; set; }
    }

    public class MovieModel
    {
        public int MovieId { get; set; }
        public string MovieTitle { get; set; }
        public string MovieOriginalTitle { get; set; }
        public string DisplayTitle { get; set; }
        public int DistributorId { get; set; }
        public string DistributorTitle { get; set; }
        public string CombinedId { get; set; }
        public Distributor Distributor { get; set; }
    }
}