using System.Collections.Generic;
using System.Net;

namespace MovieSchedule.Networking
{
    public class RequestHeaders
    {
        private Dictionary<string, string> _additionalHeaders = new Dictionary<string, string>();
        private CookieContainer _cookies = new CookieContainer();

        public bool KeepAlive { get; set; }
        public string Referer { get; set; }
        public string UserAgent { get; set; }

        public string Accept { get; set; }
        public string AcceptEncoding { get; set; }
        public string AcceptLanguage { get; set; }

        public string ContentType { get; set; }

        public CookieContainer Cookies
        {
            get { return _cookies; }
            set { _cookies = value; }
        }

        public Dictionary<string, string> AdditionalHeaders
        {
            get { return _additionalHeaders; }
            set { _additionalHeaders = value; }
        }

        public RequestHeaders Clone()
        {
            var clone = new RequestHeaders
                {
                    KeepAlive = this.KeepAlive,
                    Referer = this.Referer,
                    Accept = this.Accept,
                    AcceptEncoding = this.AcceptEncoding,
                    AcceptLanguage = this.AcceptLanguage,
                    ContentType = this.ContentType,
                    UserAgent = this.UserAgent,
                    Cookies = this.Cookies,
                    AdditionalHeaders = this.AdditionalHeaders
                };

            if (this.AdditionalHeaders.Count > 0)
            {
                foreach (var key in this.AdditionalHeaders.Keys)
                {
                    clone.AdditionalHeaders.Add(key, this.AdditionalHeaders[key]);
                }
            }

            return clone;
        }
    }
}