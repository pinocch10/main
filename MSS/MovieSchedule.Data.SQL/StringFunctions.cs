using System;
using System.Data.SqlTypes;

namespace MovieSchedule.Data.SQL
{
    public class StringFunctions
    {
        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = false)]
        public static SqlDouble LevenshteinDistancePercentage(SqlString S1, SqlString S2)
        {
            if (S1.IsNull)
                S1 = new SqlString("");

            if (S2.IsNull)
                S2 = new SqlString("");

            String SC1 = S1.Value.ToUpper();
            String SC2 = S2.Value.ToUpper();

            int n = SC1.Length;
            int m = SC2.Length;

            int[,] d = new int[n + 1, m + 1];
            int cost = 0;

            if (n + m == 0)
            {
                return 100;
            }
            else if (n == 0)
            {
                return 0;
            }
            else if (m == 0)
            {
                return 0;
            }

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    if (SC1[i - 1] == SC2[j - 1])
                        cost = 0;
                    else
                        cost = 1;

                    d[i, j] = System.Math.Min(System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            double percentage = System.Math.Round((1.0 - ((double)d[n, m] / (double)System.Math.Max(n, m))) * 100.0, 2);
            return percentage;
        }

        [Microsoft.SqlServer.Server.SqlFunction(IsDeterministic = true, IsPrecise = false)]
        public static SqlInt32 LevenshteinDistance(SqlString S1, SqlString S2)
        {
            if (S1.IsNull || S2.IsNull)
                throw (new ArgumentNullException());

            int n = S1.Value.Length;
            int m = S2.Value.Length;

            int[,] d = new int[n + 1, m + 1];
            int cost = 0;

            if (n == 0)
                return m;
            if (m == 0)
                return n;

            for (int i = 0; i <= n; i++)
                d[i, 0] = i;

            for (int j = 0; j <= m; j++)
                d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    if (S1.Value[i - 1] == S2.Value[j - 1])
                        cost = 0;
                    else
                        cost = 1;

                    d[i, j] = System.Math.Min(System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    };
}
