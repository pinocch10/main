using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageHandler.Handler
{
    class HandlerBase : IHandler
    {
        public virtual void ProcessMessage(IMessage message)
        {
            ProcessMessages(new IMessage[] { message });
        }

        public virtual void ProcessMessages(IMessage[] messages)
        {
            throw new NotImplementedException();
        }
    }
}
