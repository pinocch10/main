using MessageHandler.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageHandler
{
    class Program
    {
        static void Main(string[] args)
        {
            IMessage msg1 = new Message1() { Text = "Message 1" };
            IMessage msg2 = new Message2() { Text = "Message 2" };

            HandlerManager.ProcessMessage(msg1);
            HandlerManager.ProcessMessage(msg2);

            Console.ReadKey();
        }
    }
}
