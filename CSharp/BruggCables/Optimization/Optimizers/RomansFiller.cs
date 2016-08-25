using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optimization.DataModel;

namespace Optimization.Optimizers
{
    public class RomansFiller : Filler
    {
        private const int maxCombinations = 5000;

        private DateTime earliestDate;
        private Project[] requiredProjects;
        private List<Project> potentialFillers;
        private int numberOfMonths;
        private ProductionParameters prodParams;

        private List<FilledBaseline> FilledBaselines;

        public override List<FilledBaseline> Generate(Scenario scenario, Baseline baseline, ProductionParameters prodParams)
        {
            this.prodParams = prodParams;

            var projectsWithSmallBatchSizes = scenario.Projects.Where(p => p.Batches.Any(b => b.UsedWorkHours < 2)).ToArray();
            var sizes = scenario.Projects.SelectMany(p => p.Batches.Select(b => b.UsedWorkHours / 24)).OrderBy(d => d).ToArray();
            requiredProjects = scenario.Projects.Where(p => p is FixedProject || baseline.Projects.Contains(p)).OrderBy(p => p.DeliveryDate).ToArray();
            potentialFillers = scenario.Projects.Except(requiredProjects).OrderBy(p => p.DeliveryDate).ToList();

            earliestDate = scenario.Projects.Min(p => p.DeliveryDate);
            numberOfMonths = prodParams.PlanningHorizon + 1;

            FilledBaselines = new List<FilledBaseline>();

            // calc the monly loads of all fixed project batches and the required opportunities
            var monthlyOverload = Enumerable.Range(0, numberOfMonths).Select(i => 0f).ToList();
            AddToMonthlyOverload(monthlyOverload, requiredProjects);


            // group the projects by week
            // create schedules by combining:
            Fill(requiredProjects.ToArray(), monthlyOverload, 0, 1);

            // Assign the baseline to the FilledBaselines
            foreach (var fb in FilledBaselines)
                fb.Baseline = baseline;

            return FilledBaselines;
        }
        
        /// <summary>
        /// Fill the projects chronologically
        /// </summary>
        /// <param name="projects"></param>
        private void Fill(Project[] projects, List<float> monthlyOverload, int currentMonth, int combinationCount)
        {
            int maxAmount = Math.Max(maxCombinations / combinationCount, 1);

            // find unused projects which start this month
            var thisMonthsFillers = potentialFillers.Where(p => Utils.GetMonthIndex(p.DeliveryDate) == currentMonth).Except(projects).ToArray();
            // create combinations with those projects added
            var combinations = CombinationOfProjectsForMonth(thisMonthsFillers, (1.1 - monthlyOverload[currentMonth]) * Utils.DaysInMonthIndex(currentMonth) * 24, currentMonth);
            // limit the amount we take to the best combinations
            combinations = LimitCombinationsByParams(thisMonthsFillers, combinations, maxAmount);

            // Do we need to do the next month too? If yes, make another recursion
            if (currentMonth < numberOfMonths - 1)
            {
                if (combinations.Length > 0)
                {
                    foreach (var combination in combinations)
                    {
                        var newOverload = monthlyOverload.ToList();
                        // add this combination's added projects to the overload
                        AddToMonthlyOverload(newOverload, combination);
                        Fill(projects.Union(combination).ToArray(), newOverload, currentMonth + 1, combinationCount * combinations.Length);
                    }
                }
                else
                {
                    Fill(projects, monthlyOverload, currentMonth + 1, combinationCount);
                }
            }
            else
            {
                // If we have all the months done, create a schedule for each combination and add them to the schedules list
                if (combinations.Length > 0)
                {
                    foreach (var combination in combinations)
                    {
                        var fb = new FilledBaseline()
                        {
                            Projects = projects.Union(combination).ToArray()
                        };
                        FilledBaselines.Add(fb);
                    }
                }
                else
                {
                    var fb = new FilledBaseline()
                    {
                        Projects = projects.ToArray()
                    };
                    FilledBaselines.Add(fb);
                }
            }
        }

        private Project[][] CombinationOfProjectsForMonth(Project[] projects, double upperBoundWorkhours, int month)
        {
            // Filter out the project's batches in this month
            // We combine batches of the same project into one new batch, since we can't take one of its batches but not the other
            var allProjectsUsedWorkhours = projects.Select(p =>
                    Enumerable.Range(0, p.Batches.Length)
                    .Where(b => Utils.GetMonthIndex(p.DeliveryDate.AddDays(b * 3 * 7)) == month)
                    .Sum(b => p.Batches[b].UsedWorkHours)
                ).ToArray();
            
            int maxProjects = 10; // limit combinations here already to 2^maxProjects in total.
            if (allProjectsUsedWorkhours.Count(a => a > 0) > maxProjects)
            {
                // set the workhours contribution of some projects to zero
                IOrderedEnumerable<Project> strategyOrderedProjects;
                switch (prodParams.FillerStrategy)
                {
                    case FillerPrioritizationStrategy.MaxTotalMargin:
                        strategyOrderedProjects = projects.OrderByDescending(p => p.Margin);
                        break;
                    case FillerPrioritizationStrategy.MarginHour:
                    default:
                        strategyOrderedProjects = projects.OrderByDescending(p => p.Margin / p.Batches.Sum(b => b.UsedWorkHours));
                        break;
                }
                var projectsToKeep = strategyOrderedProjects.Take(maxProjects).ToArray();
                var projectResetIndexes = Enumerable.Range(0, projects.Length).Where(i => !projectsToKeep.Contains(projects[i])).ToArray();
                foreach (var i in projectResetIndexes)
                    allProjectsUsedWorkhours[i] = 0;
            }

            // filter out the ones with 0 workhours contribution, they don't need to be in the combination.
            double[] projectsUsedWorkHours = allProjectsUsedWorkhours.Where(a => a > 0).ToArray();

            // sum the workhours and note which batches were used
            int total = Convert.ToInt32(Math.Pow(2, projectsUsedWorkHours.Count()));
            double[] workhours = new double[total];
            List<int>[] usedProjects = new List<int>[total];
            for (int i = 0; i < total; i++)
            {
                List<int> usedProjectsForCombination = new List<int>();
                double totalWorkhours = 0;
                for (int j = projectsUsedWorkHours.Count() - 1; j > -1; j--)
                {
                    int n = (i >> j) & 1;
                    if (n == 1)
                    {
                        totalWorkhours += projectsUsedWorkHours[j];
                        usedProjectsForCombination.Add(j);
                    }
                }
                if (totalWorkhours <= upperBoundWorkhours)
                {
                    workhours[i] = totalWorkhours;
                    usedProjects[i] = usedProjectsForCombination;
                }
            }

            // take only those wich are maximally 20% overload "worse" than the best overload
            var bestWorkHours = workhours.Max();
            var minWorkHours = bestWorkHours - 0.2 * upperBoundWorkhours;

            // return the combinations and their used projects
            var projectsWithWorkhours = Enumerable.Range(0, projects.Length).Where(i => allProjectsUsedWorkhours[i] > 0).Select(i => projects[i]).ToArray();
            var combinations = Enumerable.Range(0, total)
                .Where(i => workhours[i] > 0 && workhours[i] >= minWorkHours && workhours[i] <= bestWorkHours)
                .Select(i => usedProjects[i].Select(j => projectsWithWorkhours[j]).ToArray())
                .ToArray();

            return combinations;
        }

        private void AddToMonthlyOverload(List<float> monthlyOverload, Project[] projects)
        {
            var monthlyBatches = projects.SelectMany(p => Enumerable.Range(0, p.Batches.Length)
                            .Select(b => new { StartDate = p.DeliveryDate.AddDays(b * 3 * 7), Duration = p.Batches[b].UsedWorkHours }))
                            .GroupBy(b => Utils.GetMonthIndex(b.StartDate)).ToDictionary(k => k.Key, v => v.ToList());
            foreach (var o in monthlyBatches)
            {
                if (o.Key >= 0 && o.Key < monthlyOverload.Count && o.Value.Count > 0)
                {
                    float monthDivisor = DateTime.DaysInMonth(o.Value.First().StartDate.Year, o.Value.First().StartDate.Month) * 24;
                    monthlyOverload[o.Key] += o.Value.Sum(v => (float)v.Duration / monthDivisor);
                }
            }
        }

        private Project[][] LimitCombinationsByParams(Project[] thisMonthsFillers, Project[][] combinations, int maxAmount)
        {
            if (combinations.Length > maxAmount)
            {
                // first, try to apply the revenue boundary conditions and see where this gets us.
                var fillersViolatingBoundaries = thisMonthsFillers.Where(f => f.Revenue >= prodParams.FillerMinRevenue && f.Revenue <= prodParams.FillerMaxRevenue).ToArray();
                if (fillersViolatingBoundaries.Length > 0)
                {
                    var combinationsWithViolations = combinations.Where(c => fillersViolatingBoundaries.Any(f => c.Contains(f))).ToArray();
                    if (combinations.Length - combinationsWithViolations.Length < maxAmount)
                    {
                        IOrderedEnumerable<Project[]> strategyOrderedCombinationsWithViolations;
                        switch (prodParams.FillerStrategy)
                        {
                            case FillerPrioritizationStrategy.MaxTotalMargin:
                                strategyOrderedCombinationsWithViolations = combinationsWithViolations.OrderBy(o => o.Sum(p => p.Margin));
                                break;
                            case FillerPrioritizationStrategy.MarginHour:
                            default:
                                strategyOrderedCombinationsWithViolations = combinationsWithViolations.OrderBy(o => o.Sum(p => p.Margin / p.Batches.Sum(b => b.UsedWorkHours)));
                                break;
                        }
                        combinationsWithViolations = strategyOrderedCombinationsWithViolations.Take(combinations.Length - maxAmount).ToArray();
                    }
                    combinations = combinations.Except(combinationsWithViolations).ToArray();
                }

                // if still too many combinations, prioritize by strategy
                if (combinations.Length > maxAmount)
                {
                    IOrderedEnumerable<Project[]> strategyOrderedCombinations;
                    switch (prodParams.FillerStrategy)
                    {
                        case FillerPrioritizationStrategy.MaxTotalMargin:
                            strategyOrderedCombinations = combinations.OrderByDescending(o => o.Sum(p => p.Margin));
                            break;
                        case FillerPrioritizationStrategy.MarginHour:
                        default:
                            strategyOrderedCombinations = combinations.OrderByDescending(o => o.Sum(p => p.Margin / p.Batches.Sum(b => b.UsedWorkHours)));
                            break;
                    }
                    combinations = strategyOrderedCombinations.Take(maxAmount).ToArray();
                }
            }

            return combinations;
        }
    }
}
