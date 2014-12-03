//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MovieSchedule.Data
{
    using System;
    using System.Collections.Generic;
    
    public partial class User
    {
        public User()
        {
            this.Features = new HashSet<Feature>();
        }
    
        public int Id { get; set; }
        public string Name { get; set; }
        public string PasswordHash { get; set; }
        public System.DateTime Created { get; set; }
        public string Email { get; set; }
        public Nullable<int> DistributorId { get; set; }
    
        public virtual ICollection<Feature> Features { get; set; }
        public virtual Distributor Distributor { get; set; }
    }
}
