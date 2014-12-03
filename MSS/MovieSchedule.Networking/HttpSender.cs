using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MovieSchedule.Core.Logging;
using MovieSchedule.Core.Managers;

namespace MovieSchedule.Networking
{
    public class HtmlResponse
    {
        private Dictionary<string, string> _responseHeaders = new Dictionary<string, string>();

        public string Content { get; set; }

        public Dictionary<string, string> ResponseHeaders
        {
            get { return _responseHeaders; }
            set { _responseHeaders = value; }
        }

        public CookieCollection ResponseCookies { get; set; }

        public bool Success { get; set; }

        public HttpStatusCode ResponseCode { get; set; }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    public class HttpSender : ILoggable
    {
        public static HtmlNode GetHtmlNodeResponse(string url,
            string parameters = "",
            int current = 0,
            int retry = 3,
            Uri proxy = null,
            NetworkCredential credentials = null,
            Encoding encoding = null,
            RequestHeaders headers = null,
            bool intelligentRetry = true)
        {
            var response = GetHtmlResponse(url, parameters, current, retry, proxy, credentials, encoding, headers, intelligentRetry);
            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(response.Content);
                return doc.DocumentNode;
            }
            new Logger().GetDefaultLogger().ErrorFormat("Error getting response {0}, HTTP code {1}", url, response.ResponseCode);
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                new Logger().GetDefaultLogger().Error(response.Content);
            }
            return null;
        }

        public static HtmlResponse GetHtmlResponse(
            string url,
            string parameters = "",
            int current = 0,
            int retry = 3,
            Uri proxy = null,
            NetworkCredential credentials = null,
            Encoding encoding = null,
            RequestHeaders headers = null,
            bool intelligentRetry = true)
        {
            var result = new HtmlResponse() { Success = true };
            if (headers == null)
                headers = Defaults.Headers;
            if (encoding == null)
                encoding = Encoding.Default;

            var request = (HttpWebRequest)WebRequest.Create(url);
            bool success = false;
            try
            {
                PopulateHeaders(headers, request);
                if (!String.IsNullOrEmpty(parameters))
                {
                    request.Method = "POST";
                    var data = Encoding.UTF8.GetBytes(parameters);
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = data.Length;

                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }

                if (credentials != null)
                    request.Credentials = credentials;

                if (proxy != null)
                {
                    request.Proxy = new WebProxy(proxy);
                }
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    result.ResponseCookies = response.Cookies;
                    for (int i = 0; i < response.Headers.Count; ++i)
                    {
                        result.ResponseHeaders.Add(response.Headers.Keys[i], response.Headers[i]);
                    }
                    result.ResponseCode = response.StatusCode;
                    string resultString = null;
                    using (var s = response.GetResponseStream())
                    {
                        if (s != null)
                        {
                            s.ReadTimeout = 10000;
                            if (response.ContentEncoding == "gzip")
                            {
                                using (var gzipStream = new GZipStream(s, CompressionMode.Decompress))
                                {
                                    using (var sr = new StreamReader(gzipStream, encoding))
                                    {
                                        resultString = sr.ReadToEnd();
                                    }
                                }
                            }
                            else
                            {
                                using (var sr = new StreamReader(s, encoding))
                                {
                                    resultString = sr.ReadToEnd();
                                }
                            }

                        }
                    }
                    result.Content = resultString;
                    success = true;
                    return result;
                }
            }
            catch (WebException e)
            {
                result.Success = false;
                var resultString = "";
                if (e.Response != null)
                {
                    var errorWebResponse = e.Response as HttpWebResponse;
                    if (errorWebResponse != null)
                        result.ResponseCode = errorWebResponse.StatusCode;
                    using (var s = e.Response.GetResponseStream())
                    {
                        if (s != null)
                        {
                            s.ReadTimeout = 10000;
                            //using (var gzipStream = new GZipStream(s, CompressionMode.Decompress))
                            {
                                using (var sr = new StreamReader(s, Encoding.Default))
                                {
                                    resultString = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                    new Logger().GetLogger("CommunicationLogger").Error(url);
                    new Logger().GetLogger("CommunicationLogger").Error(resultString);
                }
                result.Success = false;
                result.Content = resultString;
            }
            catch (Exception ex)
            {
                new Logger().GetDefaultLogger()
                    .Error(
                        string.Format("Exception occurred during downloading: {0}, params: {1}, attempt: {2}",
                            url, string.IsNullOrWhiteSpace(parameters) ? "N/A" : parameters, current), ex);
            }
            finally
            {
                if (!success)
                {
                    if (current < retry)
                    {
                        if (intelligentRetry)
                        {
                            var teimoutMax = (current + 1) * 10;
                            SleepManager.SleepRandomTimeout(teimoutMax - 5, teimoutMax);
                        }
                        result = GetHtmlResponse(url, parameters: parameters, current: ++current, retry: retry, proxy: proxy,
                                               credentials: credentials, encoding: encoding, headers: headers, intelligentRetry: intelligentRetry);
                    }
                }
            }
            return result;
        }

        private static void PopulateHeaders(RequestHeaders headers, HttpWebRequest request)
        {
            request.KeepAlive = headers.KeepAlive;
            if (!string.IsNullOrWhiteSpace(headers.UserAgent))
                request.UserAgent = headers.UserAgent;
            if (!string.IsNullOrWhiteSpace(headers.Accept))
                request.Accept = headers.Accept;
            if (!string.IsNullOrWhiteSpace(headers.AcceptEncoding))
                request.Headers["Accept-Encoding"] = headers.AcceptEncoding;
            if (!string.IsNullOrWhiteSpace(headers.AcceptLanguage))
                request.Headers["Accept-Language"] = headers.AcceptLanguage;

            if (!string.IsNullOrWhiteSpace(headers.Accept))
                request.Accept = headers.Accept;
            if (!string.IsNullOrWhiteSpace(headers.Accept))
                request.Accept = headers.Accept;

            if (!string.IsNullOrEmpty(headers.Referer))
                request.Referer = headers.Referer;

            if (headers.AdditionalHeaders.Count > 0)
            {
                foreach (var item in headers.AdditionalHeaders)
                {
                    request.Headers[item.Key] = item.Value;
                }
            }

            request.CookieContainer = headers.Cookies;


        }
    }

}
