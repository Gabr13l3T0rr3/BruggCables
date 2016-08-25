using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optimization.DataModel;
using Gurobi;

namespace Optimization.Optimizers
{
    class ILPOptimizerFlexibleBatches : Optimizer
    {
        public override Schedule Generate(Scenario scenario, FilledBaseline baseline = null)
        {
            var opportunities = scenario.Projects.Where(p => p is Opportunity).ToArray();
            var batches = scenario.Projects.SelectMany(p => p.Batches).ToArray();
            var batchesBothLines = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Both).ToArray();
            var batchesLine1 = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Line1).ToArray();
            var batchesLine2 = batches.Where(b => b.Compatibility == Batch.LineCompatibility.Line2).ToArray();

            int bBc = batchesBothLines.Count();
            var earliestDate = scenario.Projects.Min(p => p.DeliveryDate);
            var lastDate = scenario.Projects.Max(p => p.DeliveryDate.AddDays(7 * p.Batches.Count())).AddDays(7 * 3); // add an additional buffer of 3 weeks.
            double maxDays = (lastDate - earliestDate).Days;
            var maxWeek = maxDays / 7 + 2;
            const int weeksBuffer = 3;

            const double numericScale = 100d;

            const double delayPenaltyMultiplier = 0.01; // 1% of revenue per week
            const double interestPenaltyMultiplier = (0.045 / 52.14);  // 4.5%  / 52.14 * (revenue – margin) 


            var env = new GRBEnv();
            env.Set(GRB.IntParam.UpdateMode, 1);
            env.Set(GRB.IntParam.LogToConsole, 1);
            //env.Set(GRB.IntParam.ScaleFlag, 2);
            //env.Set(GRB.IntParam.Presolve, 0);
            //env.Set(GRB.DoubleParam.TimeLimit, _config.TimeLimit.Value);
            //env.Set(GRB.DoubleParam.MIPGap, 0.02);

            var m = new GRBModel(env);

            // decide: which batch is when, on which line
            // [batch, time]
            var varBatchTimeLine1 = m.AddVars(bBc + batchesLine1.Count(), bBc + batchesLine1.Count(), 0, 1, GRB.BINARY, "varBatchTimeLine1");
            var varBatchTimeLine2 = m.AddVars(bBc + batchesLine2.Count(), bBc + batchesLine2.Count(), 0, 1, GRB.BINARY, "varBatchTimeLine2");
            // what are the time differences between times
            var varBatchTimeDiffsLine1 = m.AddVars(bBc + batchesLine1.Count() - 1, 0, maxDays, GRB.CONTINUOUS, "varBatchTimeDiffsLine1");
            var varBatchTimeDiffsLine2 = m.AddVars(bBc + batchesLine2.Count() - 1, 0, maxDays, GRB.CONTINUOUS, "varBatchTimeDiffsLine2");
            var varLineDecision = m.AddVars(bBc, 0, 1, GRB.BINARY, "varLineDecision"); // 0 = Line1, 1 = Line2


            // make stupid solution:
            // assign fixed projects, set the rest as unused
            m.Update();
            for (int l = 0; l < 2; l++)
            {
                var line = l == 0 ? varBatchTimeLine1 : varBatchTimeLine2;
                var diffs = l == 0 ? varBatchTimeDiffsLine1 : varBatchTimeDiffsLine2;
                for (int i = 0; i < diffs.GetLength(0); i++)
                    diffs[i].Set(GRB.DoubleAttr.Start, 0);
                    //m.AddConstr(diffs[i] == 0, "initial solution diffs");

                int lineT = 0;
                for (int bi = bBc; bi < line.GetLength(0); bi++)
                {
                    var batch = (l == 0 ? batchesLine1 : batchesLine2)[bi - bBc];
                    bool isFixedProject = scenario.Projects.First(p => p.Batches.Contains(batch)) is FixedProject;
                    if (isFixedProject)
                    {
                        line[bi, lineT].Set(GRB.DoubleAttr.Start, 1);
                        //m.AddConstr(line[bi, lineT] == 1, "assign batches of project in succession");
                        for (int j = 0; j < line.GetLength(1); j++)
                            if (j != lineT)
                                line[bi, j].Set(GRB.DoubleAttr.Start, 0);
                        //m.AddConstr(line[bi, j] == 0, "initial solution");
                        lineT++;
                    }
                    else
                    {
                        for (int j = 0; j < line.GetLength(1); j++)
                            line[bi, j].Set(GRB.DoubleAttr.Start, 0);
                        //m.AddConstr(line[bi, j] == 0, "initial solution");
                    }
                }

                // make zeros also for both line batches
                for (int i = 0; i < bBc; i++)
                    for (int j = 0; j < line.GetLength(1); j++)
                        line[i, j].Set(GRB.DoubleAttr.Start, 0);
                //m.AddConstr(line[i, j] == 0, "initial solution");
            }


            // line constraints:

            // assign batch only once (constraint for single lines then both lines)
            // if it's a fixed project though, then it must be allocated
            for (int l = 0; l < 2; l++)
            {
                var line = l == 0 ? varBatchTimeLine1 : varBatchTimeLine2;
                for (int bi = bBc; bi < line.GetLength(0); bi++)
                {
                    var batchTotal = new GRBLinExpr();
                    for (int t = 0; t < line.GetLength(1); t++)
                    {
                        batchTotal += line[bi, t];
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
                for (int t = 0; t < varBatchTimeLine1.GetLength(1); t++)
                    batchTotal += varBatchTimeLine1[bi, t];
                for (int t = 0; t < varBatchTimeLine2.GetLength(1); t++)
                    batchTotal += varBatchTimeLine2[bi, t];

                var batch = batchesBothLines[bi];
                bool isFixedProject = scenario.Projects.First(p => p.Batches.Contains(batch)) is FixedProject;
                if (!isFixedProject)
                    m.AddConstr(batchTotal <= 1, "assign batch only once");
                else
                    m.AddConstr(batchTotal == 1, "assign batch exactly once");
            }

            // a time slot can only be used once on each line
            for (int l = 0; l < 2; l++)
            {
                var line = l == 0 ? varBatchTimeLine1 : varBatchTimeLine2;
                for (int t = 0; t < line.GetLength(1); t++)
                {
                    var timeTotal = new GRBLinExpr();
                    for (int bi = 0; bi < line.GetLength(0); bi++)
                    {
                         timeTotal += line[bi, t];
                    }
                    m.AddConstr(timeTotal <= 1, "assign time slot only once on line " + (l+1).ToString());
                }
            }


            // for all batches which aren't assigned to a line yet, limit the allocation of yet unassigned batches of a project to one line
            // TODO: If project has e.g. line1 and both lines, limit both lines to line 1?
            for (int pi = 0; pi < scenario.Projects.Count(); pi++)
            {
                var p = scenario.Projects[pi];

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
                            m.AddConstr(varLineDecision[i] == varLineDecision[i_prev], "lineDecisionRestrictionAllSame");
                        }
                    }
                    else
                    {
                        // if there are other batches on this project which are already on a specific line, limit to the same line
                        m.AddConstr(varLineDecision[i] == lineRestriction, "lineDecisionRestrictionSpecific");
                    }
                    i_prev = i;
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
                        for (int ti = 0; ti < varBatchTimeLine1.GetLength(1); ti++)
                            batchTotal += varBatchTimeLine1[bIndex, ti];
                    }
                    else if (b.Compatibility == Batch.LineCompatibility.Line2)
                    {
                        var bIndex = bBc + batchesLine2.IndexOf(b);
                        for (int ti = 0; ti < varBatchTimeLine2.GetLength(1); ti++)
                            batchTotal += varBatchTimeLine2[bIndex, ti];
                    }
                    else
                    {
                        var bIndex = batchesBothLines.IndexOf(b);
                        for (int t = 0; t < varBatchTimeLine1.GetLength(1); t++)
                            batchTotal += varBatchTimeLine1[bIndex, t];
                        for (int t = 0; t < varBatchTimeLine2.GetLength(1); t++)
                            batchTotal += varBatchTimeLine2[bIndex, t];
                    }

                    // the sum of this batch over all times (0 or 1) has to be the same as the sum of the previous one
                    if (bi > 0)
                    {
                        //m.AddConstr(previousBatchTotal == batchTotal, "allBatchesAllocatedOrNot");
                    }

                    previousBatchTotal = batchTotal;
                    allBatches.Add(batchTotal);
                }
                totalBatchesOfProject.Add(p, allBatches);
            }

            // Tbd: Only half of the batch slots of line 1 may be occupied. Sometimes existst as internal projects.
            // fill gap between 50% and internal projects, monthly resolution

            // gather the time of time slots
            GRBLinExpr[] startTimesLine1 = new GRBLinExpr[varBatchTimeLine1.GetLength(1)];
            GRBLinExpr[] startTimesLine2 = new GRBLinExpr[varBatchTimeLine2.GetLength(1)];
            var varStartTimesLine1 = m.AddVars(startTimesLine1.Count(), 0, maxDays, GRB.CONTINUOUS, "varStartTimesLine1");
            var varStartTimesLine2 = m.AddVars(startTimesLine2.Count(), 0, maxDays, GRB.CONTINUOUS, "varStartTimesLine2");
            for (int l = 0; l < 2; l++)
            {
                var batchLine = l == 0 ? varBatchTimeLine1 : varBatchTimeLine2;
                var startTimes = l == 0 ? startTimesLine1 : startTimesLine2;
                var batchesLine = l == 0 ? batchesLine1 : batchesLine2;
                var batchTimeDiffs = l == 0 ? varBatchTimeDiffsLine1 : varBatchTimeDiffsLine2;

                for (int t = 0; t < batchLine.GetLength(1); t++)
                {
                    startTimes[t] = new GRBLinExpr();
                    // sum up all durations and buffer times before this time instance
                    for (int previous_t = 0; previous_t < t; previous_t++)
                    {
                        var duration_prev_t = new GRBLinExpr();
                        for (int bi = 0; bi < batchLine.GetLength(0); bi++)
                        {
                            var batch = bi < bBc ? batchesBothLines[bi] : batchesLine[bi];
                            var duration = batch.UsedWorkHours / 24d;
                            duration_prev_t += batchLine[bi, previous_t] * duration;
                        }
                        duration_prev_t += batchTimeDiffs[previous_t];

                        startTimes[t] += duration_prev_t;
                    }
                }

                var varStartTime = l == 0 ? varStartTimesLine1 : varStartTimesLine2;
                for (int i = 0; i < varStartTime.Count(); i++)
                {
                    m.AddConstr(varStartTime[i] == startTimes[i], "varStartTimeAssignment");
                }
            }


            // gather the start time of the batches
            var varStartTimesBatches1 = m.AddVars(varBatchTimeLine1.GetLength(0), 0, maxDays, GRB.CONTINUOUS, "varBatchStartTimesLine1");
            var varStartTimesBatches2 = m.AddVars(varBatchTimeLine2.GetLength(0), 0, maxDays, GRB.CONTINUOUS, "varBatchStartTimesLine2");
            for (int l = 0; l < 2; l++)
            {
                var batchLine = l == 0 ? varBatchTimeLine1 : varBatchTimeLine2;
                var batchStartTimes = l == 0 ? varStartTimesBatches1 : varStartTimesBatches2;
                var batchesLine = l == 0 ? batchesLine1 : batchesLine2;
                var varStartTimes = l == 0 ? varStartTimesLine1 : varStartTimesLine2;

                for (int bi = 0; bi < batchLine.GetLength(0); bi++)
                {
                    // make the batch take the value of its time slot
                    for (int ti = 0; ti < batchLine.GetLength(1); ti++)
                    {
                        m.AddConstr(batchStartTimes[bi] <= varStartTimes[ti] + maxDays * (1 - batchLine[bi, ti]), "batchTimeAssignment");
                        m.AddConstr(batchStartTimes[bi] >= varStartTimes[ti] - maxDays * (1 - batchLine[bi, ti]), "batchTimeAssignment");
                    }
                }
            }


            // scheduling formulation
            for (int l = 0; l < 2; l++)
            {
                var varBatchStartTimes = l == 0 ? varStartTimesBatches1 : varStartTimesBatches2;
                var line = batchesBothLines.ToList();
                line.AddRange(l == 0 ? batchesLine1 : batchesLine2);

                for (int bi1 = 0; bi1 < line.Count() - 1; bi1++)
                {
                    var batch1 = line[bi1];
                    var o1_exists = opportunities.FirstOrDefault(o => o.Batches.Contains(batch1));
                    var oi1 = o1_exists != null ? (1 - totalBatchesOfProject[o1_exists][0]) : new GRBLinExpr();
                    var s1 = varBatchStartTimes[bi1];
                    var bothLineSlack1 = (bi1 < bBc) ? (l == 0 ? varLineDecision[bi1] - 0 : 1 - varLineDecision[bi1]) : new GRBLinExpr();
                    for (int bi2 = bi1 + 1; bi2 < line.Count(); bi2++)
                    {
                        var batch2 = line[bi2];
                        var o2_exists = opportunities.FirstOrDefault(o => o.Batches.Contains(batch2));
                        var oi2 = o2_exists != null ? (1 - totalBatchesOfProject[o1_exists][0]) : new GRBLinExpr();
                        var s2 = varBatchStartTimes[bi2];
                        var bothLineSlack2 = (bi2 < bBc) ? (l == 0 ? varLineDecision[bi2] - l : l - varLineDecision[bi2]) : new GRBLinExpr();

                        // S1 - E2 >= 0 OR S2 - E1 >= 0
                        // IF both batches are used
                        var decisionVar = m.AddVar(0, 1, GRB.BINARY, "schedulingORvar");
                        var opportunityNotUsedSlack = oi1 + oi2;
                        var varBothLinesLineDecisionSlack = bothLineSlack1 + bothLineSlack2; // LineDecisionSlack for both lines batches, i.e. if a bothline batch is used, don't restrict unused line.
                        m.AddConstr(s1 - (s2 + batch2.UsedWorkHours / 24d) >= -maxDays * (decisionVar + opportunityNotUsedSlack + varBothLinesLineDecisionSlack), "batchesScheduling");
                        m.AddConstr(s2 - (s1 + batch1.UsedWorkHours / 24d) >= -maxDays * (1 - decisionVar + opportunityNotUsedSlack + varBothLinesLineDecisionSlack), "batchesScheduling");
                    }
                }
            }


            // Maximize the margin (including delay and interest penalties) and the workload
            // Only the delivery of the first batch is really important. The rest is less important.

            var margin = new GRBLinExpr();
            var delays = new List<GRBLinExpr>();
            var interests = new List<GRBLinExpr>();
            var weekValues = new List<GRBLinExpr>();
            var weekDiffs = new List<GRBLinExpr>();
            foreach (var p in scenario.Projects)
            {
                // if the project is used add the margin
                if (p is Opportunity)
                {
                    margin += totalBatchesOfProject[p][0] * p.Margin * (1d / numericScale);
                }
                else
                {
                    margin += p.Margin / numericScale;
                }
                
                // deduct the delay penalty for each batch.
                var startDayOfProject = (p.DeliveryDate - earliestDate).TotalDays;
                var varMaxedValue = m.AddVars(p.Batches.Count(), 0, maxWeek, GRB.CONTINUOUS, "penaltyIndicator" + p.Description);
                //var varDecisionVar = m.AddVars(p.Batches.Count(), 0, 1, GRB.BINARY, "penaltyIndicator" + p.Description);

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
                        weekValue = varStartTimesBatches1[bi] / 7d;
                    }
                    else if (batchesLine2.Contains(b))
                    {
                        var bi = batchesLine2.IndexOf(b) + bBc;
                        weekValue = varStartTimesBatches2[bi] / 7d;
                    }
                    else
                    {
                        var bi = batchesBothLines.IndexOf(b);
                        // create a new var
                        var bothLineWeekVar = m.AddVar(0, maxWeek, GRB.CONTINUOUS, "weekValueBothLines");
                        //m.Update();
                        m.AddConstr(bothLineWeekVar >= varStartTimesBatches1[bi] / 7d - (varLineDecision[bi]) * maxWeek, "weekValueBothLinesDefinitionByConstraints");
                        m.AddConstr(bothLineWeekVar <= varStartTimesBatches1[bi] / 7d + (varLineDecision[bi]) * maxWeek, "weekValueBothLinesDefinitionByConstraints");
                        m.AddConstr(bothLineWeekVar >= varStartTimesBatches2[bi] / 7d - (1 - varLineDecision[bi]) * maxWeek, "weekValueBothLinesDefinitionByConstraints");
                        m.AddConstr(bothLineWeekVar <= varStartTimesBatches2[bi] / 7d + (1 - varLineDecision[bi]) * maxWeek, "weekValueBothLinesDefinitionByConstraints");
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
                            var weekDiffIfUsed = m.AddVar(-maxWeek, maxWeek, GRB.CONTINUOUS, "weekDiffIfUsed");
                            //m.Update();
                            var wD = weekValue - plannedWeek;
                            m.AddConstr(weekDiffIfUsed >= wD - (1 - totalBatchesOfProject[p][0]) * maxWeek, "weekDiffConstraintDefinition1");
                            m.AddConstr(weekDiffIfUsed <= wD + (1 - totalBatchesOfProject[p][0]) * maxWeek, "weekDiffConstraintDefinition2");
                            m.AddConstr(weekDiffIfUsed <= (totalBatchesOfProject[p][0]) * maxWeek, "weekDiffConstraintDefinition3");
                            m.AddConstr(weekDiffIfUsed >= -(totalBatchesOfProject[p][0]) * maxWeek, "weekDiffConstraintDefinition4");
                            weekDiff = weekDiffIfUsed;
                        }
                        else
                        {
                            weekDiff = weekValue - plannedWeek;
                        }

                        m.AddConstr(varMaxedValue[pbi] >= weekDiff, "maxedWeekDiffValueConstraintDefinition");
                        //m.AddConstr(varMaxedValue[pbi] >= 0, "maxedWeekDiffValueConstraintDefinition");
                        //m.AddConstr(varMaxedValue[pbi] <= weekDiff + maxWeek * (varDecisionVar[pbi]), "maxedWeekDiffValueConstraintDefinition");
                        //m.AddConstr(varMaxedValue[pbi] <= 0 + maxWeek * (1 - varDecisionVar[pbi]), "maxedWeekDiffValueConstraintDefinition");


                        double firstBatchImportance = pbi == 0 ? 1 : 0.2; // mainly the first batch is important, the rest is meh
                        weekValues.Add(weekValue);
                        weekDiffs.Add(weekDiff);
                        double revenueBase = p.Revenue == 0 ? 1E+8 : p.Revenue; // if it's an internal project it must not be shifted, therefore we make the penalty high
                        revenueBase /= numericScale;
                        delays.Add(varMaxedValue[pbi] * delayPenaltyMultiplier * revenueBase * firstBatchImportance);
                        interests.Add(-(weekDiff - varMaxedValue[pbi]) * interestPenaltyMultiplier * revenueBase * firstBatchImportance);
                        margin -= delays.Last() + interests.Last();
                    }

                    // constraint: batches of a project have to be in succession, i.e. batch2 can't come after batch3 chronologically
                    if (pbi > 0) m.AddConstr(weekValue - previousWeekValue >= 0, "batchesOfProjectHaveToBeInSuccession");
                    previousWeekValue = weekValue;
                }
            }



            //m.SetObjective(margin, GRB.MAXIMIZE);

            m.Update();
            m.Write("ilproman3.mps");
            m.Write("ilproman3.mst");

            //m.Tune();
            //m.GetTuneResult(0);
            m.Optimize();

            //env.Set(GRB.IntParam.IISMethod, 0); // makes IIS computation fast but potentially inaccurate
            m.ComputeIIS();
            m.Write("ilp.ilp");


            // TODO: Max 5 weeks delay per project


            // build the solution from the optimization
            var sol = new Schedule();
            int batchCount = 0;
            double cumulatedDelays = 0;
            double cumulatedInterests = 0;
            foreach (var p in scenario.Projects)
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
                        dayLine1 = varStartTimesBatches1[bBc + batchesLine1.IndexOf(b)].Get(GRB.DoubleAttr.X);
                    }
                    else if (b.Compatibility == Batch.LineCompatibility.Line2)
                    {
                        dayLine2 = varStartTimesBatches2[bBc + batchesLine2.IndexOf(b)].Get(GRB.DoubleAttr.X);
                    }
                    else
                    {
                        var bbi = batchesBothLines.IndexOf(b);
                        var lineDecision = varLineDecision[bbi].Get(GRB.DoubleAttr.X);
                        dayLine1 = varStartTimesBatches1[bbi].Get(GRB.DoubleAttr.X) * (1 - lineDecision);
                        dayLine2 = varStartTimesBatches2[bbi].Get(GRB.DoubleAttr.X) * lineDecision;
                    }

                    data.Add(new double[] { delay, interest, weekValue, weekDiff, dayLine1 + dayLine2 });

                    if (dayLine1 > 0 && dayLine2 > 0)
                        throw new InvalidOperationException();

                    var alloc = Schedule.LineAllocation.None;
                    if (p is FixedProject || (p is Opportunity && totalBatchesOfProject[p][0].Value > 0.5))
                    {
                        if (b.Compatibility == Batch.LineCompatibility.Both)
                        {
                            alloc = varLineDecision[batchesBothLines.IndexOf(b)].Get(GRB.DoubleAttr.X) > 0.5 ? Schedule.LineAllocation.Line2 : Schedule.LineAllocation.Line1;
                        }
                        else
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

            /*var maxMustWinDate = mustWinOpportunitites.Max(p => p.DeliveryDate.AddDays(7 * p.Batches.Count())).AddDays(7 * 3);
            var minMustWinDate = mustWinOpportunitites.Min(p2 => p2.DeliveryDate);
            var sevenmonthsrange = sol.Where(p => p.Project.DeliveryDate >= minMustWinDate && p.Project.DeliveryDate <= maxMustWinDate).ToArray();
            var ops = sevenmonthsrange.Where(p => mustWinOpportunitites.Contains(p.Project) || p.Project is FixedProject);
            var s = ops.Sum(p => p.Project.Revenue);*/

            return sol;
        }
    }
}
