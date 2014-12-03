using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using MovieSchedule.Data;

namespace MovieSchedule.Parsers.Common
{
    public class CinemaVariantDeterminator
    {
        public CinemaVariant FromLink(Link cinemaLink, City currentCity, string targetSite)
        {
            string cinemaName = cinemaLink.Text;
            string cinemaNameStripped = null;
            string possibleCity = null;
            var regex = new Regex(@"[/(]([\sà-ÿÀ-ß/-]*)[/)]");
            var match = regex.Match(cinemaName);
            if (match.Success)
            {
                possibleCity = match.Groups[1].Value;
                cinemaNameStripped = cinemaName.Replace(match.Groups[0].Value, string.Empty).Trim();
            }
            return new CinemaVariant
                {
                    City = currentCity.Name,
                    CinemaLink = cinemaLink,
                    CinemaNameStripped = cinemaNameStripped,
                    PossibleCity = possibleCity,
                    TargetSite = targetSite
                };
        }

        public Cinema DetermineCinemaVariant(Link cinemaLink, City currentCity, string targetSite)
        {
            var variant = FromLink(cinemaLink, currentCity, targetSite);
            return DetermineCinemaVariant(variant);
        }

        public Cinema DetermineCinemaVariant(CinemaVariant choice)
        {
            return string.IsNullOrWhiteSpace(choice.PossibleCity)
                       ? DetermineCinemaVariantWithoutPossibleCity(choice)
                       : DetermineCinemaVariantWithPossibleCity(choice);
        }

        private Cinema DetermineCinemaVariantWithoutPossibleCity(CinemaVariant choice)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var cinema = model.Cinemas.Include(x => x.Sources).Include(x => x.City).FirstOrDefault(x =>
                                                                                                       (x.Name == choice.CinemaLink.Text && x.City.Name == choice.City));
                if (cinema != null) return cinema;
                cinema = model.Cinemas.Include(x => x.Sources).Include(x => x.City).FirstOrDefault(x =>
                                                                                                   (x.City.Name == choice.City && x.Sources.Any(xx => xx.Text == choice.CinemaLink.Text)));
                return cinema;
            }
        }

        private Cinema DetermineCinemaVariantWithPossibleCity(CinemaVariant choice)
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var cinema = model.Cinemas.Include(x => x.Sources).Include(x => x.City).FirstOrDefault(x =>
                                                                                                       (x.Name == choice.CinemaLink.Text && x.City.Name == choice.City)
                                                                                                       || (x.Name == choice.CinemaLink.Text && x.City.Name == choice.PossibleCity)
                                                                                                       || (x.Name == choice.CinemaNameStripped && x.City.Name == choice.City)
                                                                                                       || (x.Name == choice.CinemaNameStripped && x.City.Name == choice.PossibleCity));
                if (cinema != null)
                {
                    var cinemaSource = model.Sources.Include(x => x.Cinema).Include(x => x.City).FirstOrDefault(x =>
                                                                                                                x.CinemaId == cinema.Id &&
                                                                                                                ((x.TargetSite == choice.TargetSite && x.Text == choice.CinemaLink.Text && x.Cinema.City.Name == choice.City)
                                                                                                                 || (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaLink.Text && x.Cinema.City.Name == choice.PossibleCity)
                                                                                                                 || (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaNameStripped && x.Cinema.City.Name == choice.City)
                                                                                                                 || (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaNameStripped && x.Cinema.City.Name == choice.PossibleCity)));
                    if (cinemaSource == null)
                    {
                    }
                    return cinema;
                }
                var source = model.Sources.Include(x => x.Cinema).Include(x => x.City).FirstOrDefault(x =>
                                                                                                      (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaLink.Text && x.Cinema.City.Name == choice.City)
                                                                                                      || (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaLink.Text && x.Cinema.City.Name == choice.PossibleCity)
                                                                                                      || (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaNameStripped && x.Cinema.City.Name == choice.City)
                                                                                                      || (x.TargetSite == choice.TargetSite && x.Text == choice.CinemaNameStripped && x.Cinema.City.Name == choice.PossibleCity));
                if (source != null)
                    return source.Cinema;
            }
            return null;
        }

    }
}