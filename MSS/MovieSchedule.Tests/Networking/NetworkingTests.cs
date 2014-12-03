using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MovieSchedule.Tests.Networking
{
    [TestClass]
    public class NetworkingTests
    {
        [TestMethod]
        public void HttpSender500Response()
        {
            var erroneousResponse = MovieSchedule.Networking.HttpSender.GetHtmlResponse("http://httpstat.us/503", intelligentRetry: false);
            Assert.IsNotNull(erroneousResponse, "Response should be present anyway");
            Assert.IsFalse(erroneousResponse.Success);
            Console.WriteLine(erroneousResponse.Success);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, erroneousResponse.ResponseCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(erroneousResponse.Content));
            Console.WriteLine(erroneousResponse.Content);
        }
    }
}
