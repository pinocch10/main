namespace MovieSchedule.Parsers.Common
{
    public class CinemaVariant
    {
        public Link CinemaLink { get; set; }
        public string CinemaNameStripped { get; set; }
        public string City { get; set; }
        public string PossibleCity { get; set; }
        public string TargetSite { get; set; }
    }
}