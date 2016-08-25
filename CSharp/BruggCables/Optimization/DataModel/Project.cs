using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Project
    {
        public readonly int Nr;
        [JsonConverter(typeof(HtmlEncodeConverter))]
        public readonly string Description;
        public readonly DateTime DeliveryDate;
        [JsonIgnore]
        public readonly Batch[] Batches;
        public readonly double Revenue;
        public readonly double Margin;

        protected Project(int nr, string descr, DateTime deliveryDate, Batch[] batches, double revenue, double margin)
        {
            Nr = nr;
            Description = descr;
            DeliveryDate = deliveryDate;
            Batches = batches;
            Revenue = revenue;
            Margin = margin;
        }


        // Equal overrides

        public override int GetHashCode() => Nr;

        public override bool Equals(object obj)
        {
            Project article = obj as Project;
            if (article == null)
            {
                return false;
            }
            else
            {
                return Nr == article.Nr;
            }
        }

        public static bool operator ==(Project _lhs, Project _rhs)
        {
            if (ReferenceEquals(_lhs, _rhs))
                return true;

            if (ReferenceEquals(_lhs, null) || ReferenceEquals(_rhs, null))
                return false;

            return _lhs.Nr == _rhs.Nr;
        }

        public static bool operator !=(Project _lhs, Project _rhs)
        {
            return !(_lhs == _rhs);
        }

        public override string ToString()
        {
            return Nr.ToString();
        }
    }
}
