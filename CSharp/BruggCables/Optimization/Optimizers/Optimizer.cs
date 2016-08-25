using Optimization.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.Optimizers
{
    /// <summary>
    /// The point of the optimizer is to create a schedule with optimal margin (including fillers), with or without an underlying baseline.
    /// </summary>
    public abstract class Optimizer
    {
        public abstract Schedule Generate(Scenario scenario, FilledBaseline baseline = null);
    }
}
