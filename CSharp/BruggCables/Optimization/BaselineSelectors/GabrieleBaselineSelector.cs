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
        public const double overLoadRatio = 1.1;

        public const double baselineMinRevenue = 1500000d;
        public const double baselineMaxRevenue = 6000000d;

        private Dictionary<int, List<Project>> groupedByWeek;

        private List<Project[]> baselines;

        public override List<Baseline> GenerateBaselines(Scenario scenario, List<Opportunity> priorities = null)
        {
            var projects = new List<Project>();

            if (priorities != null)
            {
                projects = scenario.Projects.Except(priorities).ToList();
            } else { 
                projects = scenario.Projects.Select(p => p).ToList();
            }

            var numberMonths = getNumberMonths(projects);
            var startDate = getStartDate(projects);
            var schedDays = getNumberDays(projects);

            // First Selection -> selection
            var firstSelection = ComputeFirstSelection(projects, startDate);

            // Second Selection -> combination
            var baseLines = GetSecondCombination(projects, firstSelection, priorities, startDate, schedDays);
        
            var bl = baseLines.Select(b => new Baseline(b.Select(p => (Project)p).ToList())).ToList();
            /* foreach (Baseline b in bl)
            {
                Console.WriteLine(b.CalculateTotalMargin());
                Console.WriteLine(b.CalculateTotalDaysOfOverlay(scenario));
                Console.WriteLine();
            }*/
            return bl;
        }


        private List<List<List<Opportunity>>> ComputeFirstSelection(List<Project> projects, DateTime startDate)
        {
            var firstSelection = new List<List<List<Opportunity>>>();
            var numberMonths = getNumberMonths(projects);

            for (int currMont = 0; currMont < numberMonths; currMont++)
            {
                var lowBound = startDate.AddMonths(currMont);
                var highBound = startDate.AddMonths(currMont + 1);
                var monthDays = (float)(highBound - lowBound).TotalDays;

                var fixProj = projects.Where(p => p.DeliveryDate >= lowBound && p.DeliveryDate < highBound && p is FixedProject).
                                                Select(p => (FixedProject)p).ToArray();

                var openOff = projects.Where(p => p.Revenue >= baselineMinRevenue && p.Revenue <= baselineMaxRevenue &&
                                                           p.DeliveryDate >= lowBound && p.DeliveryDate < highBound && p is Opportunity).
                                                Select(p => (Opportunity)p).ToArray();

                if (openOff.Count() > 0)
                {
                    firstSelection.Add(GetFirstCombination(fixProj, openOff, monthDays));
                }
            }
            return firstSelection;
        }


        private List<List<Opportunity>> GetFirstCombination(FixedProject[] fixProj, Opportunity[] openOff, float monthDays)
        {

            //production limit
            var availableHoursL1 = monthDays * 24.0;
            var availableHoursL2 = monthDays * 24.0;

            //projects workload 
            var projLoadL1 = fixProj.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line1).Select(p => p.Batches[0].UsedWorkHours).Sum();
            var projLoadL2 = fixProj.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).Select(p => p.Batches[0].UsedWorkHours).Sum();
            var projLoadBt = fixProj.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Both).Select(p => p.Batches[0].UsedWorkHours).Sum();

            var fisrtComb = new List<List<Opportunity>>();

            if (openOff.Count() > 0)
            {
                var offComb = PowerSetGeneration(openOff);
                for (int i = offComb.Count() - 1; i > 0; i--)
                {
                    var offLoadL1 = offComb[i].Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line1).Select(p => p.Batches[0].UsedWorkHours).Sum();
                    var offLoadL2 = offComb[i].Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).Select(p => p.Batches[0].UsedWorkHours).Sum();
                    var offLoadBt = offComb[i].Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Both).Select(p => p.Batches[0].UsedWorkHours).Sum();

                    var totLoadL1 = projLoadL1 + offLoadL1;
                    var totLoadL2 = projLoadL2 + offLoadL2;
                    var totLoadBt = projLoadBt + offLoadBt;

                    if (totLoadL1 <= availableHoursL1 || totLoadL2 <= availableHoursL1 || totLoadL1 + totLoadL2 + totLoadBt <= availableHoursL1 + availableHoursL2)
                    {
                        fisrtComb.Add(offComb[i]);
                    }
                }
            }
            return fisrtComb;
        }



        private List<List<Opportunity>> GetSecondCombination(List<Project> projects, List<List<List<Opportunity>>> firstSelection, List<Opportunity> priorities, DateTime startDate, double schedDays) {

            var nClusters = firstSelection.Count();
            var secondCombination = new List<List<Opportunity>>();
            var fixProj = projects.Where(p => p is FixedProject).Select(p => (FixedProject)p).ToArray();

            int[] nItems = firstSelection.Select(list => list.Count()).ToArray();
            int nComb = nItems.Aggregate((acc, val) => acc * val);

            var indices = new int[nClusters];

            for (int iter = 0; iter < nComb; iter++){

                var offSel = new List<List<Opportunity>>();
                for (int i = 0; i < nClusters; i++){offSel.Add(firstSelection[i][indices[i]]);}

                var selection = offSel.SelectMany(x => x).ToList();
                if (priorities != null){ selection.AddRange(priorities); }
                                        
                var wlDistr = getWorkloadDistribution(projects, selection, fixProj, startDate);

                if (checkLoadDistributionSecSel(projects, wlDistr, schedDays) == true){ secondCombination.Add(selection); }

                // update rule
                indices[0] += 1;
                for (int i = 0; i < nClusters-1; i++) {
                    if (indices[i] >= nItems[i]) {
                        indices[i] = 0;
                        indices[i + 1] += 1;
                    }
                }
            }
            return secondCombination;
        }


        private bool checkLoadDistributionSecSel(List<Project> scenario, List<Double[]> wlDistr, double schedDays) {

            var numberMonths = getNumberMonths(scenario);
            // check on the local workload (< 1.1 * Available Hours)
            var schedHoursL1 = schedDays * 24;
            var schedHoursL2 = schedDays * 24;

            bool checkLocalHours = true;
            double totalworkload = 0.0;

            for (int icheck = 0; icheck < numberMonths; icheck++)
            {
                // check the local workload distribution
                if (wlDistr[0][icheck] > schedHoursL1 * overLoadRatio &&
                    wlDistr[1][icheck] > schedHoursL2 * overLoadRatio)
                {
                    checkLocalHours = false;
                }

                // check the local workload distribution
                if (wlDistr[0][icheck] + wlDistr[1][icheck] + wlDistr[2][icheck] >
                    (schedHoursL1 + schedHoursL2) * overLoadRatio)
                {
                    checkLocalHours = false;
                }

                totalworkload += wlDistr[0][icheck] + wlDistr[1][icheck] + wlDistr[2][icheck];

            }

            // check the total amount of hours associated to the selection
            if (totalworkload > (schedHoursL1 + schedHoursL2) * numberMonths)
            {
                checkLocalHours = false;
            }
            return checkLocalHours;
        }


        private List<double[]> getWorkloadDistribution(List<Project> scenario, List<Opportunity> selection, FixedProject[] projects, DateTime startDate) {

            var workLoad = new List<Double[]>();

            var selOppL1 = selection.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line1).ToArray();
            var selProL1 = projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line1).ToArray();
            workLoad.Add(DistributeWorkHours(scenario, selOppL1, selProL1, startDate));

            var selOppL2 = selection.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).ToArray();
            var selProL2 = projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).ToArray();
            workLoad.Add(DistributeWorkHours(scenario, selOppL2, selProL2, startDate));

            var selOppBt = selection.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Both).ToArray();
            var selProBt = projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Both).ToArray();
            workLoad.Add(DistributeWorkHours(scenario, selOppBt, selProBt, startDate));

            return workLoad;
        }


        private Double[] DistributeWorkHours(List<Project> scenario,Opportunity[] selection, FixedProject[] projects, DateTime startDate) {

            var numberMonths = getNumberMonths(scenario);
            var workLoad = new Double[(int)numberMonths];
           
            if (selection.Count() > 0) {
                for (int i = 0; i < selection.Count(); i++) {
                    var startCl = ((selection[i].DeliveryDate.Year - startDate.Year) * 12) + selection[i].DeliveryDate.Month - startDate.Month;
                    var nBatch = selection[i].Batches.Count();
                    for (int currCl = startCl; currCl < Math.Min(startCl + nBatch, numberMonths); currCl++) {
                        workLoad[currCl] += selection[i].Batches[currCl - startCl].UsedWorkHours;
                    }
                }
            }

            if (projects.Count() > 0) {
                for (int i = 0; i < projects.Count(); i++) {
                    var startCl = ((projects[i].DeliveryDate.Year - startDate.Year) * 12) + projects[i].DeliveryDate.Month - startDate.Month;
                    var nBatch = projects[i].Batches.Count();
                    for (int currCl = startCl; currCl < Math.Min(startCl + nBatch, numberMonths); currCl++){
                        workLoad[currCl] += projects[i].Batches[currCl - startCl].UsedWorkHours;
                    }
                }
            }

            return workLoad;
        }

        private List<List<Opportunity>> PowerSetGeneration(Opportunity[] openOff){

            var PwSet = new List<List<Opportunity>>();

            for (int i = 0; i < (1 << openOff.Count()); i++){

                var sublist = new List<Opportunity>();

                for (int j = 0; j < openOff.Count(); j++){  
                    if ((i & (1 << j)) != 0){ sublist.Add(openOff[j]); }
                }
                PwSet.Add(sublist);            
            }
            return PwSet;
        }


        public int getNumberMonths(List<Project> projects)
        {
            DateTime lowerBound = projects.Select(p => p.DeliveryDate).Min();
            DateTime upperBound = projects.Select(p => p.DeliveryDate).Max();
            return ((upperBound.Year - lowerBound.Year) * 12) + upperBound.Month - lowerBound.Month;
        }


        public int getNumberDays(List<Project> projects)
        {
            DateTime lowerBound = projects.Select(p => p.DeliveryDate).Min();
            DateTime upperBound = projects.Select(p => p.DeliveryDate).Max();
            var totDays = (upperBound- lowerBound).Days;
            return totDays;
        }


        public DateTime getStartDate(List<Project> projects) {
            var lowerBound = projects.Select(p => p.DeliveryDate).Min();
            var startOfMonth = new DateTime(lowerBound.Year, lowerBound.Month, 1);
            return startOfMonth;
        }

    }
}