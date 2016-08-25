using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optimization.DataModel;

namespace Optimization.BaselineSelectors
{
    public class GabrieleBaselineSelector : BaselineSelector
    {
        public const int numberOfWeeks = 12 * 4;
        public const double baselineMinRevenue = 1500000d;
        public const double baselineMaxRevenue = 6000000d;

        private Dictionary<int, List<Project>> groupedByWeek;
        private DateTime earliestDate;

        private List<Project[]> baselines;

        public override List<Baseline> GenerateBaselines(Scenario scenario)
        {
            baselines = new List<Project[]>();
            earliestDate = scenario.Projects.Min(p => p.DeliveryDate);

            // select only the middlerange opportunities and sort them by start time
            var projectsSortedByStartTime = scenario.Projects
                .Where(p => p.Revenue >= baselineMinRevenue && p.Revenue <= baselineMaxRevenue)
                .OrderBy(p => p.DeliveryDate);

            // group the projects by week
            groupedByWeek = projectsSortedByStartTime.GroupBy(p => (int)((p.DeliveryDate - earliestDate).TotalDays / 7d)).ToDictionary(p => p.Key, p => p.ToList());

            // create schedules by combining weekly:
            // in each weekly, if the weekly's not occupied, use each of the weekly' projects or none.
            // Start to fill recursively!
            Fill(new List<Project>(), 0);

            // We now have a list of baselines
            // TODO: Filter out infeasible baselines, for example...
            var feasibleBaselines = baselines.Where(b => b.Sum(p => p.Margin) >= 3000000d)
                .OrderByDescending(b => b.Sum(p => p.Margin))
                .Select(b => new Baseline(b.ToList()))
                .ToList();

            return feasibleBaselines;
        }

        private void Fill(List<Project> projects, int week)
        {
            if (week <= numberOfWeeks)
            {
                if (groupedByWeek.ContainsKey(week))
                {
                    foreach (var p in groupedByWeek[week])
                    {
                        var newAllocatedProjects = new List<Project>(projects);
                        newAllocatedProjects.Add(p);
                        var estimatedWeek = (int)((p.DeliveryDate.AddDays(7 * p.Batches.Count()) - earliestDate).TotalDays / 7d);
                        Fill(newAllocatedProjects, estimatedWeek);
                    }
                }
                // plus also make a schedule variation with no project allocated in this month
                Fill(projects, week + 1);
            }
            else
            {
                // we reached our scheduling limit! Add the schedule to the list
                baselines.Add(projects.ToArray());
            }
        }
    }
}
