using System;
using System.Threading;

namespace MovieSchedule.Core.Managers
{
    public class SleepManager
    {
        private const int LowerBound = 1;
        private const int UpperBound = 10;


        public static void SleepRandomTimeout(int lowerBound = 1, int upperBound = 5)
        {
            SleepRandomTimeout(lowerBound, upperBound, 1000);
        }

        /// <summary>
        /// Method to perform random thread sleep
        /// </summary>
        /// <param name="lowerBound">Lower bound, second / 10</param>
        /// <param name="upperBound">Upper bound, second / 10</param>
        /// <param name="multiplier">Multiplier to convert to seconds</param>
        public static void SleepRandomTimeout(int lowerBound, int upperBound, int multiplier = 100)
        {
            int lowerBoundLocal = lowerBound != 0 ? lowerBound : LowerBound;
            int upperBoundLocal = upperBound != 0 ? upperBound : UpperBound;
            var r = new Random();
            var sleep = r.Next(lowerBoundLocal, upperBoundLocal);
            Thread.Sleep(sleep * multiplier);
        }
    }
}