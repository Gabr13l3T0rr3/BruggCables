using Optimization.Optimizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    /// <summary>
    /// Defines a baseline with fillers in it
    /// </summary>
    public class FilledBaseline
    {
        /// <summary>
        /// Contains all opportunities, including those of the baseline, AND the fixed projects.
        /// </summary>
        public Project[] Projects;

        /// <summary>
        /// The parent baseline projects
        /// </summary>
        public Baseline Baseline;

        public Schedule CalcSchedule(Scenario scenario)
        {
            var o = new LPOptimizerIndescrete();
            return o.Generate(scenario, this);
        }

        public double CalculateTotalMargin()
        {
            return Projects.Sum(p => p.Margin);
        }

        public double CalculateTotalDaysOfOverlay()
        {
            return 0d;
        }

        public double CalculateInsecurity()
        {
            return 1d;
        }

        public bool ProjectsAreEqual(IEnumerable<int> projects)
        {
            return Projects.Select(p => p.Nr).SequenceEqual(projects);
        }
    }
}
