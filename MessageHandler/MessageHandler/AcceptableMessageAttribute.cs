using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageHandler
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class AcceptableMessageAttribute : Attribute
    {
        public Type MessageType { get; set; }
    }
}
