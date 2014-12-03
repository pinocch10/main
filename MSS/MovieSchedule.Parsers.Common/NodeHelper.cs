using System;
using System.Linq;
using System.Web;
using HtmlAgilityPack;

namespace MovieSchedule.Parsers.Common
{
    public static class NodeHelper
    {
        public static HtmlNodeCollection GetNodes(this HtmlNode node, params string[] elements)
        {
            var result = new HtmlNodeCollection(node);
            foreach (var t in from element in elements select node.SelectNodes(element) into tt where tt != null from t in tt select t)
            {
                result.Add(t);
            }
            return result.Count > 0 ? result : null;
        }

        public static string TrimDecode(this string rawData)
        {
            return HttpUtility.HtmlDecode(rawData).Trim(' ', '\r', '\n', '\t');
        }

        public static string TrimDecode(this HtmlNode node)
        {
            if (node == null) return string.Empty;
            return TrimDecode((string)node.InnerText);
        }

        public static Link ParseLink(this HtmlNode node, Uri baseUri = null)
        {
            return ParseLinkInternal(node.Name != "a" ? node.SelectSingleNode("a") : node, baseUri);
        }

        private static Link ParseLinkInternal(HtmlNode node, Uri baseUri = null)
        {
            if (node == null) return null;
            var href = node.Attributes["href"].Value;
            var uri = baseUri != null ? new Uri(baseUri, href) : new Uri(href);
            return new Link
                {
                    Reference = uri,
                    Text = TrimDecode((string)node.InnerText)
                };
        }
    }
}