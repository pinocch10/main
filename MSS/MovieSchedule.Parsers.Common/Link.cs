using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MovieSchedule.Parsers.Common
{
    [DebuggerDisplay("{Text} | {Reference.AbsoluteUri}")]
    public class Link
    {
        public Uri Reference { get; set; }
        public string Text { get; set; }
        public List<string> Parameters { get; set; }
        public Link Clone()
        {
            var clone = new Link() { Text = this.Text, Reference = this.Reference };

            if (Parameters != null)
                foreach (var parameter in Parameters)
                {
                    clone.Parameters.Add(parameter);
                }

            return clone;
        }
    }
}