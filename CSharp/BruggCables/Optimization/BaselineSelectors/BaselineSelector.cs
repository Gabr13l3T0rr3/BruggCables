using Optimization.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.BaselineSelectors
{
    public abstract class BaselineSelector
    {
        public abstract List<Baseline> GenerateBaselines(Scenario scenario, List<Opportunity> priorities = null);
    }
}
