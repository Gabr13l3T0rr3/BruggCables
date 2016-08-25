using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    /// <summary>
    /// Defines a list of projects with their allocations, like an agenda.
    /// </summary>
    public class Schedule : List<Schedule.ProjectAllocation>
    {
        public class BatchAllocation
        {
            public readonly Batch Batch;
            public readonly LineAllocation AllocatedLine;
            public readonly DateTime Start;

            public BatchAllocation(Batch b, LineAllocation allocatedLine, DateTime start)
            {
                Batch = b;
                AllocatedLine = allocatedLine;
                Start = start;
            }
            
            public override string ToString()
            {
                return $"Batch day {Start} {AllocatedLine}";
            }
        }

        public enum LineAllocation
        {
            Line1,
            Line2,
            None
        }

        public class ProjectAllocation
        {
            public readonly Project Project;
            public readonly List<Schedule.BatchAllocation> Allocations;

            public ProjectAllocation(Project p, List<Schedule.BatchAllocation> allocations)
            {
                Project = p;
                Allocations = allocations;
            }
        }
    }
}
