using MessageHandler.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageHandler.Handler
{
    [AcceptableMessage(MessageType = typeof(Message2))]
    class Handler2 : HandlerBase
    {
        public void ProcessMessage(IMessage[] messages)
        {
            messages.ToList().ForEach(m => Console.WriteLine("handler2 processing - " + m.Text));
        }
    }
}
