namespace MovieSchedule.Web.Models
{
    public class ShowtimeModel
    {
        public string Format { get; set; }
        public int Week { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string CinemaName { get; set; }
        public int SessionsCount { get; set; }
        public string Sessions { get; set; }
        public string RealFormat { get; set; }
        public int Deviation { get; set; }
        public bool IsSnapshotPresent { get; set; }
        public string SnapshotSessions { get; set; }
    }
}