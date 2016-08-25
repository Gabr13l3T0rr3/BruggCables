using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    public class Batch
    {
        public readonly double UsedWorkHours; // of 7*24
        public readonly LineCompatibility Compatibility;

        public Batch(double usedWorkHours, LineCompatibility compatibility)
        {
            UsedWorkHours = usedWorkHours;
            Compatibility = compatibility;
        }

        /// <summary>
        /// Not all cables can be produced on Line1. But all cables can be produced on Line2.
        /// A project is assign to one of the lines entirely.
        /// </summary>
        public enum LineCompatibility
        {
            Both,
            Line1,
            Line2
        }

        #region static methods
        /// <summary>
        /// Mostly comes directly from brugg cables.
        /// </summary>

        public const int DENSITY_ISOLATION = 922;  // units?
        public const int BATCH = 45000; // what's this?

        public static readonly double[] Diameters = {
            17.3, 19.3, 21.2, 23.1, 25.0, 26.7, 28.4, 30.1, 31.7, 33.3, 34.8, 36.2,
            37.6, 39.0, 40.3, 41.5, 42.8, 43.9, 47.1, 48.2, 49.2, 50.3, 51.3, 52.2,
            53.2, 54.1, 54.9, 55.8, 56.6, 57.4, 58.2, 59.0, 59.7, 60.4, 61.1, 61.8,
            62.5, 63.2, 63.8, 64.5, 65.1, 65.7, 66.4, 67.0, 67.6, 68.2, 68.8, 69.5
        };

        public static readonly int[] Area =
        {
            150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800, 850,
            900, 950, 1000, 1050, 1100, 1150, 1200, 1250, 1300, 1350, 1400, 1450, 1500,
            1550, 1600, 1650, 1700, 1750, 1800, 1850, 1900, 1950, 2000, 2050, 2100,
            2150, 2200, 2250, 2300, 2350, 2400, 2450, 2500
        };

        public static readonly double[] Voltage =
        {
            132, 220, 275, 330, 380, 420, 500
        };

        public static readonly int[] WANDST =
        {
            15, 18, 20, 22, 24, 26, 30
        };

        public static double GetDiameterFromArea(int area)
        {
            return Diameters[Array.IndexOf(Area, Area.OrderBy(a => Math.Abs(area - a)).First())];
        }

        public static int GetIsolationFromVoltage(int voltage)
        {
            return WANDST[Array.IndexOf(Voltage, Voltage.OrderBy(v => Math.Abs(voltage - v)).First())];
        }

        /// <summary>
        /// From customer meeting 03.05.2016: Target 7 days, but put max. 7.5days in a single batch.
        /// </summary>
        public const double BATCHDAYSLIMIT = 7.5 * 24;

        public const int WORKHOURS_PER_WEEK = 7 * 24;

        /// <summary>
        /// Returns amount of batches and production in each batch, calculated from cable details
        /// </summary>
        /// <param name="length"></param>
        /// <param name="voltage"></param>
        /// <param name="area"></param>
        public static Batch[] CalculateBatches(int length, int voltage, int area)
        {
            var diam = GetDiameterFromArea(area);
            var isolationTick = GetIsolationFromVoltage(voltage);
            var isolationDiameter = diam + 2 * isolationTick;
            var isolationSize = Math.Pow(isolationDiameter + 2, 2) * Math.PI / 4d - Math.Pow(diam - 3, 2) * Math.PI / 4d;

            var lineSpeed = 4266.8 * Math.Pow(isolationSize, -0.9909) * 60;
            var productionLimit = BATCH / (isolationSize / 1e6 * DENSITY_ISOLATION);
            
            var compatibility = GetCompatibility(area, voltage);
            var batches = new List<Batch>();

            if (length > productionLimit * (BATCHDAYSLIMIT / WORKHOURS_PER_WEEK))
            {
                int batchCount = (int)(length / productionLimit);
                batches.AddRange(Enumerable.Range(0, batchCount).Select(i => new Batch(productionLimit / lineSpeed, compatibility)));
                batches.Add(new Batch((length - batchCount * productionLimit) / lineSpeed, compatibility));
            }
            else
            {
                batches.Add(new Batch(length / lineSpeed, compatibility));
            }

            return batches.ToArray();
        }

        /// <summary>
        /// Returns amount of batches and production in each batch, calculated from workload "Zeit" input
        /// </summary>
        /// <param name="workload"></param>
        /// <returns></returns>
        public static Batch[] CalculateBatches(double workload, LineCompatibility line)
        {
            var batches = new List<Batch>();
            if (workload < BATCHDAYSLIMIT)
                batches.Add(new Batch(workload, line));
            else
            {
                int batchCount = (int)(workload / WORKHOURS_PER_WEEK);
                batches.AddRange(Enumerable.Range(0, batchCount).Select(i => new Batch(WORKHOURS_PER_WEEK, line)));
                batches.Add(new Batch(workload - batchCount * WORKHOURS_PER_WEEK, line));
            }
            return batches.ToArray();
        }

        public static LineCompatibility GetCompatibility(int area, double voltage)
        {
            if (voltage <= 630 && area <= 150)
                return LineCompatibility.Both;
            else
                return LineCompatibility.Line2;
        }

        #endregion
    }
}
