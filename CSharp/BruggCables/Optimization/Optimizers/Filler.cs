using Optimization.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.Optimizers
{
    public abstract class Filler
    {
        public abstract List<FilledBaseline> Generate(Scenario scenario, Baseline baseline, ProductionParameters prodParams);
    }
}
