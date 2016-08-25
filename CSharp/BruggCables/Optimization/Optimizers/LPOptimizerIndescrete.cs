using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optimization.DataModel;
using Gurobi;

namespace Optimization.Optimizers
{
    public class LPOptimizerIndescrete : Optimizer
    {
        public override Schedule Generate(Scenario scenario, FilledBaseline fillerBaseline = null)
        {
            var projects = fillerBaseline == null ? scenario.Projects : fillerBaseline.Projects;
            // TODO: With fillerBaseline use ALL OF the projects

            var opportunities = projects.Where(p => p is Opportunity).ToArray();
            var batches = projects.SelectMany(p => p.Batches).ToArray();
            var batchesBothLines = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Both).ToArray();
            var batchesLine1 = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Line1).ToArray();
            var batchesLine2 = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Line2).ToArray();

            int bBc = batchesBothLines.Count();
            var earliestDate = projects.Min(p => p.DeliveryDate);
            var lastDate = projects.Max(p => p.DeliveryDate.AddDays(7 * p.Batches.Count())).AddDays(7 * 3); // add an additional buffer of 3 weeks.
            double maxDays = (lastDate - earliestDate).Days;
            var maxWeek = maxDays / 7 + 1;
            const int weeksBuffer = 3;


            var delayPenaltyMultiplier = 0.01; // 1% of revenue per week
            var interestPenaltyMultiplier = (0.045 / 52.14);  // 4.5%  / 52.14 * (revenue – margin) 


            var env = new GRBEnv();
            env.Set(GRB.IntParam.UpdateMode, 1);
            env.Set(GRB.IntParam.LogToConsole, 1);
            //env.Set(GRB.IntParam.Threads, 1);
            //env.Set(GRB.IntParam.NumericFocus, 3);
            //env.Set(GRB.DoubleParam.TimeLimit, 30);
            env.Set(GRB.DoubleParam.MIPGap, 0.06);


            //env.Set(GRB.IntParam.Presolve,2);
            //env.Set(GRB.IntParam.ScaleFlag, 2);
            //env.Set(GRB.IntParam.Method, 2);



            var m = new GRBModel(env);

            // decide: which batch is when, on which line
            var varBatchTimeLine1 = m.AddVars(bBc + batchesLine1.Count(), 0, maxDays, GRB.CONTINUOUS, "varBatchTimeLine1");
            var varBatchTimeLine2 = m.AddVars(bBc + batchesLine2.Count(), 0, maxDays, GRB.CONTINUOUS, "varBatchTimeLine2");
            var varOpportunityIsUsed = m.AddVars(opportunities.Count(), 0, 1, GRB.BINARY, "varOpportunityIsUsed");
            var varLineDecision = m.AddVars(bBc, 0, 1, GRB.BINARY, "varLineDecision"); // 0 = Line1, 1 = Line2
                                                                                       //m.Update();

            //TODO: Start heuristic
            //m.Update();
            //varMaxedValue[0].Set(GRB.DoubleAttr.Start, 5);

            // baseline constraint:
            // we want some opportunities to be used for sure
            /*if (baseline  != null)
            {
                var mustWinOpportunitites = new List<Opportunity>();
                for (int oi = 0; oi < opportunities.Count(); oi++)
                {
                    if (baseline.Projects.Contains(opportunities[oi]))
                    {
                        mustWinOpportunitites.Add((Opportunity)opportunities[oi]);
                        m.AddConstr(varOpportunityIsUsed[opportunities.IndexOf(opportunities[oi])] == 1);
                    }
                }

            }*/


            // line constraints:
            // if it's a fixed project though, then it must be allocated
            // for all batches which aren't assigned to a line yet, limit the allocation of yet unassigned batches of a project to one line
            // TODO: If project has e.g. line1 and both lines, limit both lines to line 1?
            for (int pi = 0; pi < projects.Count(); pi++)
            {
                var p = projects[pi];

                if (!p.Batches.Any(b => b.Compatibility == Batch.LineCompatibility.Both))
                    continue;

                int lineRestriction = -1;
                if (p.Batches.Any(b => b.Compatibility != Batch.LineCompatibility.Both))
                    lineRestriction = p.Batches.First(b => b.Compatibility != Batch.LineCompatibility.Both).Compatibility == Batch.LineCompatibility.Line1 ? 0 : 1;

                int i_prev = -1;
                for (int j = 0; j < p.Batches.Count(); j++)
                {
                    if (p.Batches[j].Compatibility != Batch.LineCompatibility.Both)
                        continue;

                    var i = batchesBothLines.IndexOf(p.Batches[j]);
                    if (lineRestriction == -1)
                    {
                        if (i_prev != -1)
                        {
                            m.AddConstr(varLineDecision[i] == varLineDecision[i_prev]);
                        }
                    }
                    else
                    {
                        // if there are other batches on this project which are already on a specific line, limit to the same line
                        m.AddConstr(varLineDecision[i] == lineRestriction);
                    }
                    i_prev = i;
                }
            }

            // scheduling formulation
            for (int l = 0; l < 2; l++)
            {
                var lineVars = l == 0 ? varBatchTimeLine1 : varBatchTimeLine2;
                var line = batchesBothLines.ToList();
                line.AddRange(l == 0 ? batchesLine1 : batchesLine2);

                for (int bi1 = 0; bi1 < line.Count() - 1; bi1++)
                {
                    var batch1 = line[bi1];
                    var o1_exists = opportunities.FirstOrDefault(o => o.Batches.Contains(batch1));
                    var oi1 = o1_exists != null ? (1-varOpportunityIsUsed[opportunities.IndexOf(o1_exists)]) : new GRBLinExpr();
                    var s1 = lineVars[bi1];
                    for (int bi2 = bi1 + 1; bi2 < line.Count(); bi2++)
                    {
                        var batch2 = line[bi2];
                        var o2_exists = opportunities.FirstOrDefault(o => o.Batches.Contains(batch2));
                        var oi2 = o2_exists != null ? (1-varOpportunityIsUsed[opportunities.IndexOf(o2_exists)]) : new GRBLinExpr();
                        var s2 = lineVars[bi2];

                        // S1 - E2 >= 0 OR S2 - E1 >= 0
                        // IF both batches are used
                        var decisionVar = m.AddVar(0, 1, GRB.BINARY, "schedulingORvar");
                        var opportunityNotUsedSlack = oi1 + oi2;
                        m.AddConstr(s1 - (s2 + batch2.UsedWorkHours / 24d) >= -maxDays * (decisionVar + opportunityNotUsedSlack)); //TODO: varLineDecisionSlack for both lines batches
                        m.AddConstr(s2 - (s1 + batch1.UsedWorkHours / 24d) >= -maxDays * (1 - decisionVar + opportunityNotUsedSlack));
                    }
                }
            }

            // Tbd: Only half of the batch slots of line 1 may be occupied. Sometimes existst as internal projects.
            // fill gap between 50% and internal projects, monthly resolution



            // Maximize the margin (including delay and interest penalties) and the workload
            // Only the delivery of the first batch is really important. The rest is less important.
            var margin = new GRBLinExpr();
            var delays = new List<GRBLinExpr>();
            var interests = new List<GRBLinExpr>();
            var weekValues = new List<GRBLinExpr>();
            var weekDiffs = new List<GRBLinExpr>();
            foreach (var p in projects)
            {
                // if the project is used add the margin
                if (p is Opportunity)
                {
                    margin += varOpportunityIsUsed[opportunities.IndexOf(p)] * p.Margin;
                }
                else
                {
                    margin += p.Margin;
                }

                // deduct the delay penalty for each batch.
                var startDayOfProject = (p.DeliveryDate - earliestDate).TotalDays;
                var varMaxedValue = m.AddVars(p.Batches.Count(), 0, maxWeek, GRB.CONTINUOUS, "penaltyIndicator" + p.Description);
                var varDecisionVar = m.AddVars(p.Batches.Count(), 0, 1, GRB.BINARY, "penaltyIndicator" + p.Description);
                //m.Update();
                GRBLinExpr previousWeekValue = new GRBLinExpr();
                for (int pbi = 0; pbi < p.Batches.Count(); pbi++)
                {
                    var b = p.Batches[pbi];
                    // compare the minimal batch time (3 + 1 weeks) to the actual delivery time

                    GRBLinExpr weekValue;
                    if (batchesLine1.Contains(b))
                    {
                        var bi = batchesLine1.IndexOf(b) + bBc;
                        weekValue = varBatchTimeLine1[bi] / 7d;
                    }
                    else if (batchesLine2.Contains(b))
                    {
                        var bi = batchesLine2.IndexOf(b) + bBc;
                        weekValue = varBatchTimeLine2[bi] / 7d;
                    }
                    else
                    {
                        var bi = batchesBothLines.IndexOf(b);
                        // create a new var
                        var bothLineWeekVar = m.AddVar(0, maxWeek, GRB.CONTINUOUS, "weekValueBothLines");
                        //m.Update();
                        m.AddConstr(bothLineWeekVar >= varBatchTimeLine1[bi] / 7d - (varLineDecision[bi]) * maxWeek);
                        m.AddConstr(bothLineWeekVar <= varBatchTimeLine1[bi] / 7d + (varLineDecision[bi]) * maxWeek);
                        m.AddConstr(bothLineWeekVar >= varBatchTimeLine2[bi] / 7d - (1 - varLineDecision[bi]) * maxWeek);
                        m.AddConstr(bothLineWeekVar <= varBatchTimeLine2[bi] / 7d + (1 - varLineDecision[bi]) * maxWeek);
                        weekValue = bothLineWeekVar;
                    }


                    if (true || pbi < p.Batches.Count() - 1)
                    {
                        // for positive difference add delay penalty, for negative difference add interest penalty
                        // x = opportunity used ? x : 0
                        // var = max(0, x)
                        // margin += var * delay
                        // margin += (x - var) * interest
                        var plannedWeek = startDayOfProject / 7d + pbi * (weeksBuffer + 1);
                        GRBLinExpr weekDiff;
                        if (p is Opportunity)
                        {
                            var weekDiffIfUsed = m.AddVar(0, maxWeek, GRB.CONTINUOUS, "weekValueBothLines");
                            //m.Update();
                            var wD = weekValue - plannedWeek;
                            m.AddConstr(weekDiffIfUsed >= wD - (1 - varOpportunityIsUsed[opportunities.IndexOf(p)]) * maxWeek);
                            m.AddConstr(weekDiffIfUsed <= wD + (1 - varOpportunityIsUsed[opportunities.IndexOf(p)]) * maxWeek);
                            m.AddConstr(weekDiffIfUsed <= (varOpportunityIsUsed[opportunities.IndexOf(p)]) * maxWeek);
                            m.AddConstr(weekDiffIfUsed >= -(varOpportunityIsUsed[opportunities.IndexOf(p)]) * maxWeek);
                            weekDiff = weekDiffIfUsed;
                        }
                        else
                        {
                            weekDiff = weekValue - plannedWeek;
                        }

                        m.AddConstr(varMaxedValue[pbi] >= weekDiff);
                        m.AddConstr(varMaxedValue[pbi] >= 0);
                        m.AddConstr(varMaxedValue[pbi] <= weekDiff + maxWeek * (varDecisionVar[pbi]));
                        m.AddConstr(varMaxedValue[pbi] <= 0 + maxWeek * (1 - varDecisionVar[pbi]));
                        

                        double firstBatchImportance = pbi == 0 ? 1 : 0.2; // mainly the first batch is important, the rest is meh
                        weekValues.Add(weekValue);
                        weekDiffs.Add(weekDiff);
                        double revenueBase = p.Revenue == 0 ? 999999999 : p.Revenue;
                        delays.Add(varMaxedValue[pbi] * delayPenaltyMultiplier * revenueBase * firstBatchImportance);
                        interests.Add(-(weekDiff - varMaxedValue[pbi]) * interestPenaltyMultiplier * revenueBase * firstBatchImportance);
                        margin -= delays.Last() + interests.Last();
                    }

                    // constraint: batches of a project have to be in succession, i.e. batch2 can't come after batch3 chronologically
                    if (pbi > 0) m.AddConstr(weekValue - previousWeekValue >= 0);
                    previousWeekValue = weekValue;
                }
            }



            m.SetObjective(margin, GRB.MAXIMIZE);

            //m.Tune();
            //m.GetTuneResult(0);
            m.Optimize();

            //env.Set(GRB.IntParam.IISMethod, 0); // makes IIS computation fast but potentially inaccurate
            //m.ComputeIIS();
            //m.Write("ilp.ilp");


            // TODO: Max 5 weeks delay per project
            
            
            // build the solution from the optimization
            var sol = new Schedule();
            int batchCount = 0;
            double cumulatedDelays = 0;
            double cumulatedInterests = 0;
            foreach (var p in projects)
            {
                List<Schedule.BatchAllocation> allocatedBatches = new List<Schedule.BatchAllocation>();
                var data = new List<double[]>();
                for (int bi = 0; bi < p.Batches.Count(); bi++)
                {
                    var b = p.Batches[bi];
                    var delay = delays[batchCount].Value;
                    var interest = interests[batchCount].Value;
                    cumulatedDelays += delay;
                    cumulatedInterests += interest;
                    var weekValue = weekValues[batchCount].Value;
                    var weekDiff = weekDiffs[batchCount].Value;
                    // figure out on which week and which line the batch is allocated
                    double dayLine1 = 0;
                    double dayLine2 = 0;
                    if (b.Compatibility == Batch.LineCompatibility.Line1)
                    {
                        dayLine1 = varBatchTimeLine1[bBc + batchesLine1.IndexOf(b)].Get(GRB.DoubleAttr.X);
                    }
                    else if (b.Compatibility == Batch.LineCompatibility.Line2)
                    {
                        dayLine2 = varBatchTimeLine2[bBc + batchesLine2.IndexOf(b)].Get(GRB.DoubleAttr.X);
                    }
                    else
                    {
                        var bbi = batchesBothLines.IndexOf(b);
                        var lineDecision = varLineDecision[bbi].Get(GRB.DoubleAttr.X);
                        dayLine1 = varBatchTimeLine1[bbi].Get(GRB.DoubleAttr.X) * (1 - lineDecision);
                        dayLine2 = varBatchTimeLine2[bbi].Get(GRB.DoubleAttr.X) * lineDecision;
                    }

                    data.Add(new double[] { delay, interest, weekValue, weekDiff, dayLine1 + dayLine2 });

                    if (dayLine1 > 0 && dayLine2 > 0)
                        throw new InvalidOperationException();

                    var alloc = Schedule.LineAllocation.None;
                    if (p is FixedProject || (p is Opportunity && varOpportunityIsUsed[opportunities.IndexOf(p)].Get(GRB.DoubleAttr.X) > 0.5))
                    {
                        if (b.Compatibility == Batch.LineCompatibility.Both)
                        {
                            alloc = varLineDecision[batchesBothLines.IndexOf(b)].Get(GRB.DoubleAttr.X) > 0.5 ? Schedule.LineAllocation.Line2 : Schedule.LineAllocation.Line1;
                        } else
                        {
                            alloc = b.Compatibility == Batch.LineCompatibility.Line1 ? Schedule.LineAllocation.Line1 : Schedule.LineAllocation.Line2;
                        }

                    }
                    //if (dayLine1 > 0) alloc = Solution.LineAllocation.Line1;
                    //if (dayLine2 > 0) alloc = Solution.LineAllocation.Line2;

                    allocatedBatches.Add(new Schedule.BatchAllocation(b, alloc, earliestDate.AddDays(dayLine1 + dayLine2)));
                    batchCount++;
                }
                sol.Add(new Schedule.ProjectAllocation(p, allocatedBatches));
            }

            return sol;
        }
    }
}
