using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageHandler
{
    class HandlerManager
    {
        private static List<HandlerAttributes> _handlerTypes; //cache

        public static void Init()
        {
            _handlerTypes = new List<HandlerAttributes>();

            Type type = typeof(IHandler);
            List<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p)).ToList();

            foreach (Type t in types)
            {
                if (t == typeof(IHandler)) continue;

                IHandler handler = Activator.CreateInstance(t, null, null) as IHandler;
                RegisterHandler(handler);
            }
        }

        public static void RegisterHandler(IHandler handler)
        {
            if (handler == null) return;

            HandlerAttributes attr = new HandlerAttributes();
            attr.Handler = handler;
            attr.Attributes = (AcceptableMessageAttribute[])handler.GetType().GetCustomAttributes(typeof(AcceptableMessageAttribute), true);

            _handlerTypes.Add(attr);
        }

        public static void ProcessMessage(IMessage msg)
        {
            if(_handlerTypes == null)
            {
                Init();
            }

            Type msgType = msg.GetType();

            IHandler handler = _handlerTypes.FirstOrDefault(t => t.Attributes.Any(a => a.MessageType == msgType)).Handler;

            handler.ProcessMessage(msg);
        }

        public static void ProcessMessages(IMessage[] messages)
        {
            if (_handlerTypes == null)
            {
                Init();
            }

            var types = messages.Select(m => m.GetType());

            IHandler handler = _handlerTypes.FirstOrDefault(t => t.Attributes.Any(a => a.MessageType == msgType)).Handler;

            handler.ProcessMessage(msg);

            foreach (IMessage msg in messages)
            {
                Type msgType = msg.GetType();

                IHandler handler = _handlerTypes.FirstOrDefault(t => t.Attributes.Any(a => a.MessageType == msgType)).Handler;

                handler.ProcessMessage(msg);
            }
        }

        private class HandlerAttributes
        {
            public IHandler Handler { get; set; }
            public AcceptableMessageAttribute[] Attributes { get; set; }
        }
    }
}
