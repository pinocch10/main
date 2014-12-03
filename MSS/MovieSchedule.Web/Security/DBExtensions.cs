using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MovieSchedule.Data;
using MovieSchedule.Web.Models.User;

using System.Data.Entity;

namespace MovieSchedule.Web.Security
{
    public static class DBExtensions
    {

        public static List<Showtime> GetSnapshots(this MovieScheduleStatsEntities db, int movieId, DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Thursday) return new List<Showtime>();
            var yesterday = date.AddDays(-1);

            var snapshots = (from x in db.Showtimes
                             where
                                 x.MovieId == movieId
                                 && x.Date == yesterday

                             orderby x.Date descending
                             select x).Include(x => x.TargetSite).ToList();

            return snapshots;
        }

        public static List<ShowtimeSnapshot> GetSnapshots(this MovieScheduleStatsEntities db, int movieId)
        {
            var maxDate = (from x in db.ShowtimeSnapshots
                           where x.MovieId == movieId
                           group x by x.Date into g
                           orderby g.Max(x => x.Date) descending
                           select new { Date = g.Max(x => x.Date) }).FirstOrDefault();

            if (maxDate == null) return new List<ShowtimeSnapshot>();

            var snapshots = (from x in db.ShowtimeSnapshots
                             where
                                 x.MovieId == movieId
                                 && x.Date == maxDate.Date
                             orderby x.Date descending
                             select x).ToList();

            return snapshots;
        }

        public static List<ShowtimeSnapshot> GetSnapshots(this MovieScheduleStatsEntities db, int movieId, int cinemaId)
        {
            var maxDate = (from x in db.ShowtimeSnapshots
                           where x.MovieId == movieId && x.CinemaId == cinemaId
                           group x by x.Date into g
                           orderby g.Max(x => x.Date) descending
                           select new { Date = g.Max(x => x.Date) }).FirstOrDefault();

            if (maxDate == null) return new List<ShowtimeSnapshot>();

            var snapshots = (from x in db.ShowtimeSnapshots
                             where
                                 x.MovieId == movieId
                                 && x.Date == maxDate.Date
                                 && x.CinemaId == cinemaId
                             orderby x.Date descending
                             select x).ToList();

            return snapshots;
        }

        public static DateTime GetEarliestDate(this Movie movie)
        {
            var releaseDate = movie.ReleaseDate.Date;
            var previewDate = movie.PreviewDate ?? movie.ReleaseDate.Date;

            return releaseDate < previewDate ? releaseDate : previewDate;
        }

        public static int RegisterUser(this MovieScheduleStatsEntities db, User user)
        {
            var clonedUser = new User
            {
                Name = user.Name,
                PasswordHash = user.PasswordHash,
                Email = user.Email
            };
            db.Users.Add(clonedUser);
            db.SaveChanges();

            return clonedUser.Id;
        }

        public static User GetUserByUsernameAndPasswordHash(this MovieScheduleStatsEntities db, string userName, string passwordHash)
        {
            var user = db.Users.FirstOrDefault(x => x.Name == userName && x.PasswordHash == passwordHash);
            return user;
        }

        public static User GetUserByUsernameAndPassword(this MovieScheduleStatsEntities db, string userName, string password)
        {
            return GetUserByUsernameAndPasswordHash(db, userName, password.GetMD5Hash());
        }

        public static User GetUserById(this MovieScheduleStatsEntities db, string userId)
        {
            int userIdInt;
            int.TryParse(userId, NumberStyles.Any, CultureInfo.InvariantCulture, out userIdInt);
            return db.Users.FirstOrDefault(x => x.Id == userIdInt);
        }

        public static UserModel ToModel(this User user)
        {
            return new UserModel
            {
                Username = user.Name,
                Id = user.Id,
                Email = user.Email,
                DistributorId = user.Distributor.Id,
                DistributorName = user.Distributor.Name
            };
        }


    }
}