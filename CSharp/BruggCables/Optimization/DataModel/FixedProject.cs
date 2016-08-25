using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    [JsonObject(MemberSerialization.OptOut)]
    public class FixedProject : Project
    {
        public readonly bool IsInternal;
        public FixedProject(int nr, string descr, DateTime deliveryDate, Batch[] batches, double revenue, double margin, bool isInternal) : base(nr, descr, deliveryDate, batches, revenue, margin)
        {
            IsInternal = isInternal;
        }
    }
}
