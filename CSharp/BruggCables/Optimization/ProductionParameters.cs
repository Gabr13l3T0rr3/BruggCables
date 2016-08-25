using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization
{
    public class ProductionParameters
    {
        /// <summary>
        /// Planning in months. How many months should be considered for the optimization?
        /// </summary>
        public int PlanningHorizon = 9;

        public double DefaultBatchSize = 7;
        public Dictionary<string, double> SpecificBatchSizes = new Dictionary<string, double>();
        public double GapBetweenBatches = 3 * 7;

        /// <summary>
        /// How often line 1 should remain free.
        /// Value in % from [0..1]
        /// Slots for cleaning, repairing/replacing, testing, etc.
        /// </summary>
        public double DeadTimes = 0.5;

        public double MaxDelayPerYear = 7 * 2;
        public double MaxIndividualDelay = 7;
        public double MaxAdvancePerYear = 7 * 5;
        public double MaxIndividualAdvance = 7 * 3;

        public double WeeklyDelayInterest = 0.01; // 1% of revenue per week
        public double WeeklyAdvanceInterest = (0.045 / 52.14);  // 4.5%  / 52.14 * (revenue – margin)

        public double BaselineMinRevenue = 1500000d;
        public double BaselineMaxRevenue = 6000000d;

        public FillerPrioritizationStrategy FillerStrategy = FillerPrioritizationStrategy.MarginHour;
        public double FillerMaxRevenue = 1500000d;
        public double FillerMinRevenue = 0d;
        // TODO: Maybe preferred workhours min max?
    }

    public enum FillerPrioritizationStrategy
    {
        MarginHour,
        MaxTotalMargin
    }
}
