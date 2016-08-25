using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization
{
    public static class Utils
    {
        private static DateTime baseDateTime = new DateTime(2010, 1, 1);

        static Utils()
        {
            var lookupTableSize = (DateTime.Now.Year - baseDateTime.Year + 5) * 12;
            daysOfMonthLookupTable = Enumerable.Range(0, lookupTableSize).Select(m => baseDateTime.AddMonths(m)).Select(dt => DateTime.DaysInMonth(dt.Year, dt.Month)).ToArray();
        }

        private static int baseInt = (baseDateTime.Month + baseDateTime.Year * 12);
        /// <summary>
        /// Returns an int representation of the date's month, with 0 for the baseDateTime
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static int GetMonthIndex(DateTime date)
        {
            return (date.Month + date.Year * 12) - baseInt;
        }

        private static int[] daysOfMonthLookupTable;
        /// <summary>
        /// positive number, starting from baseDateTime
        /// </summary>
        /// <param name="month"></param>
        /// <returns></returns>
        public static int DaysInMonthIndex(int month)
        {
            return daysOfMonthLookupTable[month];
        }
    }
}
