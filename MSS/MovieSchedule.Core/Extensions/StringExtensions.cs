using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MovieSchedule.Core.Extensions
{
    public static class StringExtensions
    {
        const string ForbiddenSymbols = "<>:\"/\\|?*";


        public static bool IsLateSession(this string sessionTime)
        {
            return sessionTime.StartsWith("00")
                || sessionTime.StartsWith("01")
                || sessionTime.StartsWith("02")
                || sessionTime.StartsWith("03");
        }

        public static string RemoveForbiddenSymbols(this string input)
        {
            var sb = new StringBuilder(input);
            foreach (var index in ForbiddenSymbols.Select(ch => input.IndexOf(ch)).Where(index => index >= 0))
            {
                sb[index] = '_';
            }
            return sb.ToString();
        }

        public static string RemoveValueInBrackets(this string cinemaName, string value)
        {
            return Regex.Replace(cinemaName, string.Format("\\({0}\\)", value), string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        public static string ReplaceBadChar(this string cinemaName)
        {
            cinemaName = Regex.Replace(cinemaName, @"([/(][\w])*([¸])([\w]*[/)])", "$1å$3");
            cinemaName = Regex.Replace(cinemaName, @"([/(][\w])*([¨])([\w]*[/)])", "$1Å$3");

            return cinemaName;
        }



        public static string GetValueInBrackets(this string cinemaName)
        {
            var regex = new Regex(@"[/(]([\sà-ÿÀ-ß/-]*)[/)]$", RegexOptions.Compiled);
            var match = regex.Match(cinemaName);
            if (match.Success)
            {
                var cityName = match.Groups[match.Groups.Count - 1].Value;
                return cityName;
            }
            return string.Empty;
        }
    }


    public static class LevenshteinDistance
    {
        public static int Compute(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (string.IsNullOrEmpty(t))
                    return 0;
                return t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 1; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[n, m];
        }
    }


}