using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optimization.DataModel;
using System.Diagnostics;

namespace Optimization.Optimizers
{
    class GabrieleOptimizer : Optimizer
    {
        public const int freeCostsTimeWindowOffers = 30;
        public const int freeCostsTimeWindowProjects = 7;

        ProductionParameters parameters = new ProductionParameters();
       
        public override Schedule Generate(Scenario scenario, FilledBaseline baseline = null)
        {
            List<Schedule.BatchAllocation> schedule = new List<Schedule.BatchAllocation>();

            var projL1 = baseline.Projects.Where(p => p.Batches[0].Compatibility != Batch.LineCompatibility.Line2).Select(p => p).ToList();
            var projL2 = baseline.Projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).Select(p => p).ToList();
            var offrL1 = baseline.Baseline.Projects.Where(p => p.Batches[0].Compatibility != Batch.LineCompatibility.Line2).Select(p => p).ToList();
            var offrL2 = baseline.Baseline.Projects.Where(p => p.Batches[0].Compatibility == Batch.LineCompatibility.Line2).Select(p => p).ToList();
            
            // LINE 1 optimization
            Console.Write("Initializing Line 1... ");
            schedule.AddRange( allocateSchedule( projL1, offrL1, getLowerBound(scenario), getAmountScheduleHours(scenario)) );
            Console.WriteLine("Done");

            // LINE 2 optimization
            Console.Write("Initializing Line 2... ");
            schedule.AddRange( allocateSchedule( projL2, offrL2, getLowerBound(scenario), getAmountScheduleHours(scenario)) );
            Console.WriteLine("Done");

            schedule.ToArray();
            // TODO: implement the simulated anealing for the Optimization
            // TODO: figure out how to return the schedule to Programs
            throw new NotImplementedException();

        }

        public List<Schedule.BatchAllocation> allocateSchedule(List<Project> projects, List<Project> offers, DateTime startDate, int scheduledHours) {

            int[] schedule = new int[scheduledHours];

            var projTuple = projectScheduleAllcoation(projects, schedule, startDate, scheduledHours);
            var offrtuple = projectScheduleAllcoation(offers, projTuple.Item2, startDate, scheduledHours);

            schedule = offrtuple.Item2;

            var agenda = projTuple.Item1;
            if (offrtuple.Item1.Count() > 0) { agenda.AddRange(offrtuple.Item1); }
           
            // duration check
            //var DurationOffr = offers.Select(p => p.Batches.Select(b => (int)b.UsedWorkHours).Sum()).Sum();
            //var SchedDur = schedule.Sum();

            return agenda;
        }

        public Tuple<List<Schedule.BatchAllocation>, int[]> projectScheduleAllcoation(List<Project> projects, int[] schedule, DateTime startDate, int scheduledHours) {

            List < Schedule.BatchAllocation > agenda = new List<Schedule.BatchAllocation>();

            foreach (Project project in projects)
            {
                for (int ibatch = 0; ibatch < project.Batches.Count(); ibatch++)
                {
                    var delivery = project.DeliveryDate.AddDays(parameters.GapBetweenBatches);

                    // time slot allocation
                    var deliveryIndex = (int)(delivery - startDate).TotalHours;
                    if (deliveryIndex > scheduledHours) { deliveryIndex = scheduledHours; }
                    var startIndex = (deliveryIndex - (int)project.Batches[ibatch].UsedWorkHours);
                    if (startIndex < 0) { startIndex = 0; }

                    // check the availability of the allocated time slot
                    var allocation = testOverload(schedule, startIndex, deliveryIndex, freeCostsTimeWindowProjects, scheduledHours);
                    for (int work = allocation.Item1; work < allocation.Item2; work++) { schedule[work] += 1; }

                    var productionBegin = startDate.AddHours(allocation.Item1);
                    var productionEnd = startDate.AddHours(allocation.Item2);

                    agenda.Add(new Schedule.BatchAllocation(project.Batches[ibatch], Schedule.LineAllocation.Line1, (productionEnd - startDate).TotalDays));
                }
            }
            return Tuple.Create(agenda, schedule);
        }

        public Tuple<int,int> testOverload(int[] schedule, int start, int end, int freeCostsTimeWindow, int scheduledHours) {

            var response = checkOverload(schedule, start, end, 0, scheduledHours);
            ProductionParameters parameters = new ProductionParameters();

            if (response == true)
            {
                var innerResponseUp = true;
                var innerResponseDown = true;

                int startUp = start, startDown = start;
                int endUp = end, endDown = end;
                double costUp = double.MaxValue, costDown = double.MaxValue;
                
                int shift = 0;

                while (innerResponseDown == true || innerResponseUp == true)
                {
                    shift += 1;

                    innerResponseUp = checkOverload(schedule, start, end, shift, scheduledHours);
                    innerResponseDown = checkOverload(schedule, start, end, -shift, scheduledHours); 

                    if (innerResponseUp == false)
                    {
                        startUp = start + shift;
                        endUp = end + shift;
                        costUp = parameters.WeeklyDelayInterest * (int)((endUp - end) / (24d * 7d));
                    }

                    if (innerResponseDown == false)
                    {
                        startDown = start - shift;
                        endDown = end - shift;
                        if ((end - endDown) > freeCostsTimeWindow * 24d)
                        {
                            var aux = (end - endDown) - freeCostsTimeWindow;
                            costDown = parameters.WeeklyAdvanceInterest * (int)(aux / (242d * 7d));
                        }
                    }
                    if (start > shift && end + shift < scheduledHours) { break; }
                }
               
                if (costDown > costUp)
                {
                    start = startUp;
                    end = endDown;
                }
                else
                {
                    start = startDown;
                    end = endDown;
                }
            }
            
            return Tuple.Create(start, end);
        }

        public bool checkOverload(int[] schedule, int start, int end, int shift, int scheduledHours) {

            bool response = false;
            var sum = 0;
            
            var newStart = start + shift;
            var newEnd = end + shift;

            if (newStart < 0 || newEnd > scheduledHours) {
                newStart = start;
                newEnd = end;
            }

            for (int work = newStart; work < newEnd; work++) { sum += schedule[work]; }

            if (sum > 0) { response = true; }

            return response;
        }

        public DateTime getLowerBound(Scenario scenario)
        {
            var lowerBound = scenario.Projects.Select(p => p.DeliveryDate).Min();
            lowerBound = new DateTime(lowerBound.Year, lowerBound.Month, 1);
            return lowerBound;
        }

        public DateTime getUpperBound(Scenario scenario)
        {
            var upperBound = scenario.Projects.Select(p => p.DeliveryDate).Max();
            upperBound = new DateTime(upperBound.AddMonths(12).Year, upperBound.AddMonths(12).Month, 1); ;
            return upperBound;
        }

        public int getAmountScheduleHours(Scenario scenario)
        {
            var lowerBound = getLowerBound(scenario);
            var upperBound = getUpperBound(scenario);

            var totHours = (upperBound - lowerBound).TotalHours;
            return (int)totHours;
        }
    }
}
