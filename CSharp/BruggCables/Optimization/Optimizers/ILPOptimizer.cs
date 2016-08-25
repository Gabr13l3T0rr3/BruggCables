using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optimization.DataModel;
using Gurobi;

namespace Optimization.Optimizers
{
    class ILPOptimizer : Optimizer
    {
        public override Schedule Generate(Scenario scenario, FilledBaseline baseline = null)
        {
            var batches = scenario.Projects.SelectMany(p => p.Batches).OrderBy(b => b.Compatibility).ToArray();
            var batchesBothLines = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Both).ToArray();
            var batchesLine1 = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Line1).ToArray();
            var batchesLine2 = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Line2).ToArray();

            int bBc = batchesBothLines.Count();
            var earliestDate = scenario.Projects.Min(p => p.DeliveryDate);
            var lastDate = scenario.Projects.Max(p => p.DeliveryDate.AddDays(7 * p.Batches.Count())).AddDays(7 * 3); // add an additional buffer of 3 weeks.
            int maxWeek = (lastDate - earliestDate).Days / 7; // 0 to 200
            const int weeksBuffer = 3;

            var delayPenaltyMultiplier = 0.01; // 1% of revenue per week
            var interestPenaltyMultiplier = (0.045 / 52.14);  // 4.5%  / 52.14 * (revenue – margin) 


            var env = new GRBEnv();
            env.Set(GRB.IntParam.LogToConsole, 1);
            //env.Set(GRB.DoubleParam.TimeLimit, _config.TimeLimit.Value);
            //env.Set(GRB.DoubleParam.MIPGap, 0.02);

            var m = new GRBModel(env);

            // decide: which batch is when, on which line
            // TODO: Batch can be over two weeks
            var varBatchWeekLine1 = m.AddVars(batchesBothLines.Count() + batchesLine1.Count(), maxWeek, 0, 1, GRB.BINARY, "batchWeekLine1");
            var varBatchWeekLine2 = m.AddVars(batchesBothLines.Count() + batchesLine2.Count(), maxWeek, 0, 1, GRB.BINARY, "batchWeekLine2");
            m.Update();

            // line constraints:

            // assign batch only once (constraint for single lines then both lines)
            // if it's a fixed project though, then it must be allocated
            for (int l = 0; l < 2; l++)
            {
                var line = l == 0 ? varBatchWeekLine1 : varBatchWeekLine2;
                for (int bi = bBc; bi < line.GetLength(0); bi++)
                {
                    var batchTotal = new GRBLinExpr();
                    for (int w = 0; w < maxWeek; w++)
                    {
                        batchTotal += line[bi, w];
                    }
                    var batch = (l == 0 ? batchesLine1 : batchesLine2)[bi - bBc];
                    bool isFixedProject = scenario.Projects.First(p => p.Batches.Contains(batch)) is FixedProject;
                    if (!isFixedProject)
                        m.AddConstr(batchTotal <= 1, "assign batch only once");
                    else
                        m.AddConstr(batchTotal == 1, "assign batch exactly once");
                }
            }
            for (int bi = 0; bi < bBc; bi++)
            {
                var batchTotal = new GRBLinExpr();
                for (int w = 0; w < maxWeek; w++)
                {
                    batchTotal += varBatchWeekLine1[bi, w];
                    batchTotal += varBatchWeekLine2[bi, w];
                }
                var batch = batchesBothLines[bi];
                bool isFixedProject = scenario.Projects.First(p => p.Batches.Contains(batch)) is FixedProject;
                if (!isFixedProject)
                    m.AddConstr(batchTotal <= 1, "assign batch only once");
                else
                    m.AddConstr(batchTotal == 1, "assign batch exactly once");
            }


            // for all batches which aren't assigned to a line yet, limit the allocation of yet unassigned batches of a project to one line
            // TODO: If project has e.g. line1 and both lines, limit both lines to line 1?
            var varLineDecision = m.AddVars(scenario.Projects.Count(), 0, 1, GRB.BINARY, "varLineDecision");
            m.Update();
            for (int pi = 0; pi < scenario.Projects.Count(); pi++)
            {
                var p = scenario.Projects[pi];

                if (!p.Batches.Any(b => b.Compatibility == Batch.LineCompatibility.Both))
                    continue;

                var sumBatchLine1 = new GRBLinExpr();
                var sumBatchLine2 = new GRBLinExpr();
                int i = batches.IndexOf(p.Batches.First());
                for (int j = 0; j < p.Batches.Count(); j++)
                {
                    if (p.Batches[j].Compatibility != Batch.LineCompatibility.Both)
                        continue;

                    for (int w = 0; w < maxWeek; w++)
                    {
                        sumBatchLine1 += varBatchWeekLine1[i + j, w];
                        sumBatchLine2 += varBatchWeekLine2[i + j, w];
                    }
                }
                m.AddConstr(sumBatchLine1 <= (1 - varLineDecision[pi]) * GRB.INFINITY);
                m.AddConstr(sumBatchLine2 <= varLineDecision[pi] * GRB.INFINITY);
            }

            // on each line the total of a week must not exceed 24*7
            for (int l = 0; l < 2; l++)
            {
                var lineVars = l == 0 ? varBatchWeekLine1 : varBatchWeekLine2;
                var line = l == 0 ? batchesLine1 : batchesLine2;
                int bBC = batchesBothLines.Count();

                for (int w = 0; w < maxWeek; w++)
                {
                    var weekTotal = new GRBLinExpr();
                    for (int b = 0; b < lineVars.GetLength(0); b++)
                    {
                        var batch = b < bBC ? batchesBothLines[b] : line[b - bBC];

                        weekTotal += lineVars[b, w] * batch.UsedWorkHours;

                        /*var test = new GRBLinExpr();
                        test += (lineVars[b] == w) * 0.1;
                        (l[b] - w) * batch.UsedWorkHours*/
                    }
                    m.AddConstr(weekTotal <= 24 * 7);
                }
            }

            // for each project, either all or no batches must be assigned
            var totalBatchesOfProject = new Dictionary<Project, List<GRBLinExpr>>();
            foreach (var p in scenario.Projects)
            {
                var allBatches = new List<GRBLinExpr>();

                // gather the total of all batches
                GRBLinExpr previousBatchTotal = null;
                for (int bi = 0; bi < p.Batches.Count(); bi++)
                {
                    var b = p.Batches[bi];
                    var batchTotal = new GRBLinExpr();
                    if (b.Compatibility == Batch.LineCompatibility.Line1)
                    {
                        var bIndex = bBc + batchesLine1.IndexOf(b);
                        for (int w = 0; w < maxWeek; w++)
                            batchTotal += varBatchWeekLine1[bIndex, w];
                    }
                    else if (b.Compatibility == Batch.LineCompatibility.Line2)
                    {
                        var bIndex = bBc + batchesLine2.IndexOf(b);
                        for (int w = 0; w < maxWeek; w++)
                            batchTotal += varBatchWeekLine2[bIndex, w];
                    }
                    else
                    {
                        var bIndex = batchesBothLines.IndexOf(b);
                        for (int w = 0; w < maxWeek; w++)
                        {
                            batchTotal += varBatchWeekLine1[bIndex, w];
                            batchTotal += varBatchWeekLine2[bIndex, w];
                        }
                    }

                    // the sum of this batch over all weeks (0 or 1) has to be the same as the sum of the previous one
                    if (bi > 0)
                    {
                        m.AddConstr(previousBatchTotal == batchTotal);
                    }

                    previousBatchTotal = batchTotal;
                    allBatches.Add(batchTotal);
                }
                totalBatchesOfProject.Add(p, allBatches);
            }

            // DEPRECATED: penalty/interest term adds this constraint with slack
            // in-between batches of the same project, 3 weeks distance should exist, otherwise there will be a penalty
            /*foreach (var p in scenario.Projects)
            {
                var bCompatibility = p.Batches.First().Compatibility;

                // for all batches of this project make sure that the previous one is at least (3 + 1) weeks away
                // in other words: the total of batches within (3 + 1) weeks must be 4 at least.
                for (int w = 0; w < maxWeek - weeksBuffer; w++)
                {
                    var batchTotalOver4Weeks = new GRBLinExpr();
                    // sum all batches over a 4 week period
                    for (int i = 0; i <= weeksBuffer; i++)
                    {
                        foreach (var b in p.Batches)
                        {
                            if (bCompatibility == Batch.LineCompatibility.Line1)
                            {
                                batchTotalOver4Weeks += varBatchWeekLine1[bBc + batchesLine1.IndexOf(b), w + i];
                            }
                            else if (bCompatibility == Batch.LineCompatibility.Line2)
                            {
                                batchTotalOver4Weeks += varBatchWeekLine2[bBc + batchesLine2.IndexOf(b), w + i];
                            }
                            else
                            {
                                var bIndex = batchesBothLines.IndexOf(b);
                                batchTotalOver4Weeks += varBatchWeekLine1[bIndex, w + i];
                                batchTotalOver4Weeks += varBatchWeekLine2[bIndex, w + i];
                            }
                        }
                    }
                    m.AddConstr(batchTotalOver4Weeks <= 1);
                }
            }*/

            // Tbd: Only half of the batch slots of line 1 may be occupied. Sometimes existst as internal projects.
            // fill gap between 50% and internal projects, monthly resolution



            // Maximize the margin (including delay and interest penalties) and the workload
            // TODO: Only first one important? no delay otherwise?
            var margin = new GRBLinExpr();
            foreach (var p in scenario.Projects)
            {
                // if the project is used add the margin
                margin += totalBatchesOfProject[p].First() * p.Margin;

                // deduct the delay penalty for each batch.
                int startWeekOfProject = (p.DeliveryDate - earliestDate).Days / 7;
                var varMaxedValue = m.AddVars(p.Batches.Count(), 0, maxWeek, GRB.CONTINUOUS, "penaltyIndicator" + p.Description);
                var varDecisionVar = m.AddVars(p.Batches.Count(), 0, 1, GRB.BINARY, "penaltyIndicator" + p.Description);
                m.Update();
                GRBLinExpr previousWeekValue = new GRBLinExpr();
                for (int bi = 0; bi < p.Batches.Count(); bi++)
                {
                    var b = p.Batches[bi];
                    // compare the minimal batch time (3 + 1 weeks) to the actual delivery time
                    var weekValue = new GRBLinExpr();
                    if (b.Compatibility == Batch.LineCompatibility.Line1)
                    {
                        var bIndex = bBc + batchesLine1.IndexOf(b);
                        for (int w = 0; w < maxWeek; w++)
                            weekValue += varBatchWeekLine1[bIndex, w] * w;
                    }
                    else if (b.Compatibility == Batch.LineCompatibility.Line2)
                    {
                        var bIndex = bBc + batchesLine2.IndexOf(b);
                        for (int w = 0; w < maxWeek; w++)
                            weekValue += varBatchWeekLine2[bIndex, w] * w;
                    }
                    else
                    {
                        var bIndex = batchesBothLines.IndexOf(b);
                        for (int w = 0; w < maxWeek; w++)
                        {
                            weekValue += varBatchWeekLine1[bIndex, w] * w;
                            weekValue += varBatchWeekLine2[bIndex, w] * w;
                        }
                    }

                    if (bi < p.Batches.Count() - 1)
                    {
                        // for positive difference add delay penalty, for negative difference add interest penalty
                        // var = max(0, x)
                        // var * delay
                        // (x - var) * interest
                        var plannedWeek = startWeekOfProject + bi * (weeksBuffer + 1);
                        var weekDiff = weekValue - plannedWeek * totalBatchesOfProject[p].First(); // here we multiply the planned week with the (0/1)-allocation-indicator to avoid penalties when the project is not assigned (and therefore weekValue = 0)
                        m.AddConstr(varMaxedValue[bi] >= weekDiff);
                        m.AddConstr(varMaxedValue[bi] >= 0);
                        m.AddConstr(varMaxedValue[bi] <= weekDiff + maxWeek * (varDecisionVar[bi]));
                        m.AddConstr(varMaxedValue[bi] <= 0 + maxWeek * (1 - varDecisionVar[bi]));

                        double firstBatchImportance = bi == 0 ? 1 : 0.2; // mainly the first batch is important, the rest is meh

                        margin += varMaxedValue[bi] * delayPenaltyMultiplier * firstBatchImportance;
                        margin += (weekDiff - varMaxedValue[bi]) * interestPenaltyMultiplier * firstBatchImportance;
                    }

                    // constraint: batches of a project have to be in succession, i.e. batch2 can't come after batch3 chronologically
                    if (bi > 0) m.AddConstr(weekValue - previousWeekValue >= 0);
                    previousWeekValue = weekValue;
                }
            }



            m.SetObjective(margin, GRB.MAXIMIZE);
            m.Update();

            m.Optimize();

            //env.Set(GRB.IntParam.IISMethod, 0); // makes IIS computation fast but potentially inaccurate
            //m.ComputeIIS();
            //m.Write("ilp.ilp");


            // build the solution from the optimization
            var sol = new Schedule();
            foreach (var p in scenario.Projects)
            {
                List<Schedule.BatchAllocation> allocatedBatches = new List<Schedule.BatchAllocation>();
                for (int bi = 0; bi < p.Batches.Count(); bi++)
                {
                    var b = p.Batches[bi];
                    // figure out on which week and which line the batch is allocated
                    int weekLine1 = 0;
                    int weekLine2 = 0;
                    if (b.Compatibility == Batch.LineCompatibility.Line1)
                    {
                        for (int w = 1; w <= maxWeek; w++)
                        {
                            weekLine1 += (int)varBatchWeekLine1[bBc + batchesLine1.IndexOf(b), w - 1].Get(GRB.DoubleAttr.X) * w;
                        }
                    }
                    else if (b.Compatibility == Batch.LineCompatibility.Line2)
                    {
                        for (int w = 1; w <= maxWeek; w++)
                        {
                            weekLine2 += (int)varBatchWeekLine2[bBc + batchesLine2.IndexOf(b), w - 1].Get(GRB.DoubleAttr.X) * w;
                        }
                    }
                    else
                    {
                        for (int w = 1; w <= maxWeek; w++)
                        {
                            weekLine1 += (int)varBatchWeekLine1[batchesBothLines.IndexOf(b), w - 1].Get(GRB.DoubleAttr.X) * w;
                            weekLine2 += (int)varBatchWeekLine2[batchesBothLines.IndexOf(b), w - 1].Get(GRB.DoubleAttr.X) * w;
                        }
                    }

                    if (weekLine1 > 0 && weekLine2 > 0)
                        throw new InvalidOperationException();

                    var alloc = Schedule.LineAllocation.None;
                    if (weekLine1 > 0) alloc = Schedule.LineAllocation.Line1;
                    if (weekLine2 > 0) alloc = Schedule.LineAllocation.Line2;

                    allocatedBatches.Add(new Schedule.BatchAllocation(b, alloc, earliestDate.AddDays(weekLine1 + weekLine2 - 1)));
                }
                sol.Add(new Schedule.ProjectAllocation(p, allocatedBatches));
            }

            return sol;
        }
    }
}
