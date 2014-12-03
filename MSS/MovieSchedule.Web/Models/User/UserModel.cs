using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MovieSchedule.Web.Models.User
{
    public class UserModel
    {
        public int Id { get; set; }
        public string Username { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }
        public string RemoteAddress { get; set; }
        public bool Remember { get; set; }
        public DateTime LogInDate { get; set; }
        public bool RememberMe { get; set; }

        public int DistributorId { get; set; }
        public string DistributorName { get; set; }
    }
}