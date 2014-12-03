using System;
using System.Web;
using HtmlAgilityPack;
using MovieSchedule.Parsers.Common;

namespace MovieSchedule.Networking
{
    public class Constants
    {
        public const string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
        public const string AcceptLanguage = "en-US,en;q=0.8,ru;q=0.6";
        public const string AcceptEncoding = "gzip,deflate,sdch";
        public const string UserAgentChrome = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/37.0.2062.103 Safari/537.36";
        public const string UserAgentFirefox = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:31.0) Gecko/20100101 Firefox/31.0";
    }

    public class Defaults
    {
        public static readonly RequestHeaders Headers = new RequestHeaders
            {
                KeepAlive = false,
                Referer = String.Empty,
                Accept = Constants.Accept,
                AcceptLanguage = Constants.AcceptLanguage,
                AcceptEncoding = Constants.AcceptEncoding,
                ContentType = String.Empty,
                UserAgent = Constants.UserAgentFirefox
            };
    }
}