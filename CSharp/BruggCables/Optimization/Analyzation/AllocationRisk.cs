using Optimization.DataModel;
using Optimization.Optimizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.Analyzation
{
    public class AllocationRisk
    {
        public static List<Coverage> TestForCoverage(Scenario s)
        {
            // build a list of batches with their start- and endtime, and chance of success (in the project)
            var batches = new List<Tuple<Batch, DateTime>>();
            foreach (var p in s.Projects)
            {
                var nextBatchStart = p.DeliveryDate;
                foreach (var b in p.Batches.Where(b => b.Compatibility != Batch.LineCompatibility.Line1))
                {
                    batches.Add(new Tuple<Batch, DateTime>(b, nextBatchStart));
                    // 3 weeks inbetween batches
                    nextBatchStart = nextBatchStart.AddHours(b.UsedWorkHours).AddDays(7 * 3);
                }
            }

            // create all feasible combinations of batches which result in a full schedule, + their probability

            //var monthGroups = batches.GroupBy(b => b.Item2.Month + b.Item2.Year * 12).ToArray();
            var batchProjects = batches.Select(b => new { B = b.Item1, F = s.Projects.First(p => p.Batches.Contains(b.Item1)) }).ToDictionary(a => a.B, a => a.F);
            var monthGroups = AllocateBatchesOntoMonth(batches, s, batchProjects);

            // for ease of calculation, sum all fixed batches of the same month into one
            foreach (var m in monthGroups)
            {
                var fixedBatches = m.Value.Where(b => batchProjects[b.Item1] is FixedProject).ToArray();
                var newLargeBatch = new Tuple<Batch, DateTime>(new Batch(fixedBatches.Sum(fb => fb.Item1.UsedWorkHours), fixedBatches.First().Item1.Compatibility), fixedBatches.First().Item2);
                foreach (var fb in fixedBatches)
                    m.Value.Remove(fb);
                m.Value.Add(newLargeBatch);
                batchProjects.Add(newLargeBatch.Item1, batchProjects[fixedBatches.First().Item1]);
            }

            var monthRisks = new List<Coverage>();

            //foreach (var month in monthGroups)
            Parallel.ForEach(monthGroups, (month) =>
            {
                var monthAndYear = new DateTime(month.Value.First().Item2.Year, month.Value.First().Item2.Month, 1);

                // make all combinations of batches which fill the month
                double monthlyRisk = 1d;
                var monthBatches = month.Value.Select(m => m.Item1).Where(b => CalcChance(batchProjects[b]) > 0).ToList();

                var totalWorkhours = monthBatches.Sum(b => b.UsedWorkHours);
                var batchesAndChances = monthBatches.Select(b => Tuple.Create(b, CalcChance(batchProjects[b]))).ToArray();
                var totalWorkhoursFixedProjects = batchesAndChances.Where(b => b.Item2 == 1).Sum(b => b.Item1.UsedWorkHours);

                // if the entirety of the batches can actually possibly cover the month...
                if (monthBatches.Sum(b => b.UsedWorkHours) >= 24 * 30)
                {
                    monthlyRisk = CalcBatchAllocationRisk(batchesAndChances, 30 * 24);
                }

                lock (monthRisks)
                {
                    monthRisks.Add(new Coverage()
                    {
                        date = monthAndYear,
                        chance = Math.Round(monthlyRisk * 1000) / 1000d
                    });
                }
            });

            return monthRisks.OrderBy(m => m.date).ToList();
        }

        private static Dictionary<int, List<Tuple<Batch, DateTime>>> AllocateBatchesOntoMonth(List<Tuple<Batch, DateTime>> batches, Scenario s, Dictionary<Batch, Project> batchProjects)
        {
            // order chronologically, then first fill up with fixed groups, then fill up with opportunities

            // group the fixed project batches by month
            var batchesFixedOrdered = batches.Where(b => batchProjects[b.Item1] is FixedProject).OrderBy(b => b.Item2).ToList();

            // Allocate the months continuously. If a fixed project doesn't fit into its month, attach the nonfitting part/duration to the a next batch
            var monthGroups = new Dictionary<int, List<Tuple<Batch, DateTime>>>();
            var startingMonth = batches.Min(b => b.Item2.Month + b.Item2.Year * 12);
            AllocateBatches(batchesFixedOrdered, monthGroups, s, batchProjects);

            // Then allocate the opportunities the same way
            var batchesOpportunityOrdered = batches.Where(b => (batchProjects[b.Item1] is Opportunity)).OrderBy(b => b.Item2).ToList();
            AllocateBatches(batchesOpportunityOrdered, monthGroups, s, batchProjects);
            
            return monthGroups;
        }

        private static void AllocateBatches(List<Tuple<Batch, DateTime>> batches, Dictionary<int, List<Tuple<Batch, DateTime>>> monthGroups, Scenario s, Dictionary<Batch, Project> batchProjects)
        {
            for (int bc = 0; bc < batches.Count; bc++)
            {
                var b = batches[bc];
                var plannedMonthIndex = Utils.GetMonthIndex(b.Item2);

                while (b != null)
                {
                    double plannedMonthsCapacity = Utils.DaysInMonthIndex(plannedMonthIndex);

                    if (!monthGroups.ContainsKey(plannedMonthIndex))
                        monthGroups[plannedMonthIndex] = new List<Tuple<Batch, DateTime>>();

                    plannedMonthsCapacity -= monthGroups[plannedMonthIndex].Where(bt => batchProjects[bt.Item1] is FixedProject).Sum(bt => bt.Item1.UsedWorkHours / 24);

                    // if there's enough capacity for the full duration, add the batch
                    if (plannedMonthsCapacity >= b.Item1.UsedWorkHours / 24)
                    {
                        monthGroups[plannedMonthIndex].Add(b);
                        b = null;
                    }
                    else if (plannedMonthsCapacity > 0)
                    {
                        // if there's not enough capacity in the month for the full duration, but for some, then split the batch
                        var durationBatch1 = plannedMonthsCapacity * 24;
                        var durationBatch2 = (b.Item1.UsedWorkHours / 24 - plannedMonthsCapacity) * 24;
                        var b1 = new Tuple<Batch, DateTime>(new Batch(durationBatch1, b.Item1.Compatibility), b.Item2);
                        var b2 = new Tuple<Batch, DateTime>(new Batch(durationBatch2, b.Item1.Compatibility), b.Item2.AddHours(durationBatch1));
                        batchProjects.Add(b1.Item1, batchProjects[b.Item1]);
                        batchProjects.Add(b2.Item1, batchProjects[b.Item1]);
                        monthGroups[plannedMonthIndex].Add(b1);
                        b = b2;
                    }
                    else
                    {
                        // no space in this month. Try next
                        plannedMonthIndex++;
                    }
                }
            }
        }

        private static double CalcChance(Project p)
        {
            return p is FixedProject ? 1d : ((Opportunity)p).ProbabilityFromPhase;
        }

        public static double CalcBatchAllocationRisk(Tuple<Batch, double>[] batchesAndChances, double hoursGoal)
        {
            // Create all possible batch combinations, with their chance of coming true and negated, in a manner of:
            // [0, 0, 0]
            // [1, 0, 0]
            // [0, 1, 0]
            // ...

            int total = Convert.ToInt32(Math.Pow(2, batchesAndChances.Count()));
            //double[] chances = new double[total];
            //double[] workhours = new double[total];
            double chance = 0;
            for (int i = 0; i < total; i++)
            {
                double combinationChance = 1d;
                double totalWorkhours = 0;
                for (int j = batchesAndChances.Count() - 1; j > -1; j--)
                {
                    int n = (i >> j) & 1;
                    combinationChance *= n == 1 ? batchesAndChances[j].Item2 : 1d - batchesAndChances[j].Item2;
                    if (n == 1)
                        totalWorkhours += batchesAndChances[j].Item1.UsedWorkHours;
                }
                //chances[i] = combinationChance;
                //workhours[i] = totalWorkhours;
                if (totalWorkhours >= hoursGoal)
                    chance += combinationChance;
            }

            //var chance = Enumerable.Range(0, total).Where(i => workhours[i] >= hoursGoal).Sum(i => chances[i]);

            return chance;
        }

        public class Coverage
        {
            public DateTime date { get; set; }
            public double chance { get; set; }
        }
    }
}
