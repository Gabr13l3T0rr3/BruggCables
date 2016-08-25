using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    public class Verifier
    {
        public static void Verify(Schedule s)
        {
            // all fixed projects are allocated
            foreach (var p in s.Where(pk => pk.Project is FixedProject))
            {
                if (p.Allocations.Any(b => b.AllocatedLine == Schedule.LineAllocation.None || b.Start != DateTime.MinValue))
                    throw new ArgumentException($"Project {p.Project.Description} is fixed but at least one batch was not allocated");
            }

            // Of an opportunity either all or no batches are allocated
            foreach (var p in s.Where(pk => pk.Project is Opportunity))
            {
                var amountAllocated = p.Allocations.Count(b => b.AllocatedLine == Schedule.LineAllocation.None);
                if (amountAllocated > 0 && amountAllocated != p.Project.Batches.Count())
                    throw new ArgumentException($"Opportunity {p.Project.Description} has some batches allocated and some not");
            }

            // the total of all weeks must not exceed 24*7
            /*var baByLines = s.Values.SelectMany(ba => ba).Where(ba => ba.AllocatedLine != Solution.LineAllocation.None).GroupBy(ba => ba.AllocatedLine);
            foreach (var bBL in baByLines)
            {
                var gbw = bBL.ToArray().GroupBy(b => b.Day);
                if (gbw.Any(wl => wl.Sum(b => b.Batch.UsedWorkHours) > 24*7))
                    throw new ArgumentException($"At least one week has more than 24*7 hours of work allocated, detected on line {bBL.Key}");
            }*/

            // the batches of a project or opportunity are all allocated on the same line
            if (s.Any(p => p.Allocations.Select(b => b.AllocatedLine).Distinct().Count() > 1))
                throw new ArgumentException($"At least one project is allocated on more than one line");

            // used batches do not overlap
            var tolerance = 1 / 60d / 24d; //1 min tolerance
            for (int l = 0; l < 2; l++)
            {
                var line = l == 0 ? Schedule.LineAllocation.Line1 : Schedule.LineAllocation.Line2;
                var timeranges = s.SelectMany(p => p.Allocations.Where(a => a.AllocatedLine == line).Select(a => new DateTime[] { a.Start, a.Start.AddDays(a.Batch.UsedWorkHours / 24d) })).OrderBy(t => t[0]).ToArray();
                for (int i = 0; i < timeranges.Count(); i++)
                {
                    var t1 = timeranges[i];
                    for (int j = i + 1; j < timeranges.Count(); j++)
                    {
                        var t2 = timeranges[j];
                        if (t2[1] > t1[0].AddDays(tolerance) && t1[1] > t2[0].AddDays(tolerance))
                        {
                            //{ endA-startA, endA - startB, endB-startA, endB - startB }
                            var overlap = new TimeSpan[] { t1[1] - t1[0], t1[1] - t2[0], t2[1] - t1[0], t2[1] - t2[0] }.Min();
                            //Console.WriteLine(overlap);
                            throw new ArgumentException($"There's an overlap between batches on the same line over {overlap} days");
                        }
                    }
                }

            }

            // A gap of 3 weeks should exist inbetween batches of a project, if the project is actually allocated
            /*foreach (var p in s.Where(sp => sp.Value.First().AllocatedLine != Solution.LineAllocation.None))
            {
                for (int bi = 0; bi < p.Value.Count - 1; bi++)
                {
                    var b1 = p.Value[bi];
                    var b2 = p.Value[bi + 1];
                    if (b2.Day - b1.Day <= 3)
                        throw new ArgumentException($"Between batches {bi} and {bi+1} of project {p.Key.Description} is a gap of only {b2.Day - b1.Day} weeks");
                }
            }*/
        }
    }
}
