namespace MovieSchedule.Web.Models
{
    public class FilterShowtimesCompare : FilterShowtimes
    {
        public int SecondMovieId { get; set; }
    }

    public class FilterShowtimes
    {
        public int MovieId { get; set; }

        public string SessionDate { get; set; }

        public int FederalDistrictId { get; set; }

        public int CityId { get; set; }

        public int CinemaId { get; set; }

        public int CinemaNetworkId { get; set; }

        public int CountryId { get; set; }
    }
}