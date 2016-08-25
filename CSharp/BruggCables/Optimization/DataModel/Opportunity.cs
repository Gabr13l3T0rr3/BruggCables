using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Opportunity : Project
    {
        public readonly double Probability;
        public readonly double ProbabilityFromPhase;

        public Opportunity(int nr, string descr, DateTime deliveryDate, Batch[] batches, double revenue, double margin, double probability, string phase) : base(nr, descr, deliveryDate, batches, revenue, margin)
        {
            Probability = probability;
            ProbabilityFromPhase = CalculateProbabilityFromPhase(phase);
        }

        // Done according to percentage estimates input from May 2016 by Willi Naegele and Valentin Kuehle
        private double CalculateProbabilityFromPhase(string phase)
        {
            switch(phase)
            {
                case "New Opp (>> QG1)":
                case "Neue Anfrage - RFQ (>> QG1)":
                    return 0.05d;
                case "Bidding phase (>> QG2)":
                case "Angebotsphase (>> QG2)":
                    return 0.1d;
                case "Indentified Opportunity (Univers)":
                    return 0d;
                case "Negotiation/Review":
                case "Vertrags Verhandlung":
                    return 0.7d;
                default:
                    throw new InvalidOperationException("Oh, a new state?");
            }
        }
    }
}
