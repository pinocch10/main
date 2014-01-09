using MessageHandler.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageHandler.Handler
{
    [AcceptableMessage(MessageType = typeof(Message1))]
    class Handler1 : HandlerBase
    {
        public void ProcessMessage(IMessage[] messages)
        {
            messages.ToList().ForEach(m => Console.WriteLine("handler1 processing - " + m.Text));
        }
    }
}
