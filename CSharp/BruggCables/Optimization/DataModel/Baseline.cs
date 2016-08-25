using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    // NOTE: the start and end date for the scheduele computation has to be defined
    public class Baseline
    {
        const int batchShiftWeeks = 4;

        public readonly List<Project> Projects;

        public Baseline(List<Project> usedProjects)
        {
            Projects = usedProjects;
        }

        public double CalculateTotalMargin()
        {
            return Projects.Sum(p => p.Margin);
        }

        public double CalculateTotalDaysOfOverlay(Scenario scenario)
        {
            var fixProj = scenario.Projects.Where(p => p is FixedProject).Select(p => (FixedProject)p).ToList();
            
            var fixProjL1 = fixProj.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line1).Select(p => (FixedProject)p).ToList();
            var openOffrL1 = Projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line1).Select(p => (Opportunity)p).ToList();
            var openOffrBt = Projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Both).Select(p => (Opportunity)p).ToList();

            openOffrL1.AddRange(openOffrBt);

            var workLaodL1 = getWorkloadDistribution(scenario, fixProjL1, openOffrL1); 
            var DelayL1 = computeDelay(scenario, workLaodL1 ); // Delay in hours for line 1

            var fixProjL2 = fixProj.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).Select(p => (FixedProject)p).ToList();
            var openOffrL2 = Projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).Select(p => (Opportunity)p).ToList();

            var workLaodL2 = getWorkloadDistribution(scenario, fixProjL2, openOffrL2);
            var DelayL2 = computeDelay(scenario, workLaodL2); // Delay in hours for line 2

            var totDelay = (DelayL1 + DelayL2) / 24d; // Total delay in days

            return totDelay;
        }

        public double CalculateInsecurity()
        {
            return 1d;
        }

        public double computeDelay(Scenario scenario, Double[] workload)
        {
            var lowerBound = scenario.Projects.Select(p => p.DeliveryDate).Min();
            var upperBound = scenario.Projects.Select(p => p.DeliveryDate).Max();
            var nMonths = deltaInMonths(lowerBound, upperBound);

            double[] delay = new double[nMonths];
            
            for (int i = 0; i < nMonths - 1; i++) {
                var startDate = lowerBound.AddMonths(i);
                var endDate = lowerBound.AddMonths(i+1);
                var monthInHour = (endDate - startDate).TotalHours;

                double auxDelay = 0;

                if (i == 0)
                {
                    auxDelay = workload[i] - monthInHour;
                }
                else {
                    auxDelay = workload[i] - monthInHour + delay[i-1];
                }
                if (auxDelay >= 0) { delay[i] = auxDelay; }
            }


            return delay.Sum();
        }

        public double[] getWorkloadDistribution(Scenario scenario, List<FixedProject> fixProj, List<Opportunity> openOffr)
        {
            int index = 0;
            var lowerBound = scenario.Projects.Select(p => p.DeliveryDate).Min();
            var upperBound = scenario.Projects.Select(p => p.DeliveryDate).Max();

            var nMonths = deltaInMonths(lowerBound, upperBound);

            double[] workLoad = new double[nMonths];

            foreach (Project proj in fixProj)
            {
                for (int ib = 0; ib < proj.Batches.Count(); ib++)
                {
                    index = deltaInMonths(lowerBound, proj.DeliveryDate.AddDays(batchShiftWeeks * 7 * ib));
                    if (index < nMonths) { workLoad[index] += proj.Batches[ib].UsedWorkHours; }
                }

            }

            foreach (Opportunity off in openOffr)
            {
                for (int ib = 0; ib < off.Batches.Count(); ib++)
                {
                    index = deltaInMonths(lowerBound, off.DeliveryDate.AddDays(batchShiftWeeks * 7 * ib));
                    if (index < nMonths) { workLoad[index] += off.Batches[ib].UsedWorkHours; }
                }

            }

            return workLoad;
        }



        public int deltaInMonths(DateTime date1 , DateTime date2) {
             return ((date2.Year - date1.Year) * 12) + date2.Month - date1.Month;
        }
    }
}
