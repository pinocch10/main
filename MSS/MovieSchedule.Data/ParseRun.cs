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
    
    public partial class ParseRun
    {
        public ParseRun()
        {
            this.ParseRunInfoes = new HashSet<ParseRunInfo>();
            this.ShowtimeSnapshots = new HashSet<ShowtimeSnapshot>();
            this.Showtimes = new HashSet<Showtime>();
        }
    
        public int Id { get; set; }
        public System.DateTime Started { get; set; }
        public Nullable<System.DateTime> Completed { get; set; }
        public int ShowtimesCount { get; set; }
    
        public virtual ICollection<ParseRunInfo> ParseRunInfoes { get; set; }
        public virtual ICollection<ShowtimeSnapshot> ShowtimeSnapshots { get; set; }
        public virtual ICollection<Showtime> Showtimes { get; set; }
    }
}
