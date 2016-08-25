using Optimization.DataModel;
using Optimization.Optimizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Optimization.BaselineSelectors;
using static Optimization.DataModel.Schedule;
using Optimization.Analyzation;

namespace Optimization
{
    class Program
    {
        static Optimizer[] optimizers = new Optimizer[]
        {
            //new ILPOptimizerFlexibleBatches()
            new LPOptimizerIndescrete()
        };

        static void Main(string[] args)
        {
            // load scenario with all projects and opportunities
            //var s = Scenario.Load(DateTime.MinValue, DateTime.MaxValue); //DateTime.Parse("2/29/2016 12:00:00 AM"), DateTime.Parse("7/6/2016 12:00:00 AM")
            var start = new DateTime(2016, 4, 1, 0, 0, 0);
            var end = new DateTime(2017, 1, 1, 0, 0, 0);

            var s = Scenario.Load(start, end);
            ///            var priorities = s.Projects.Where(p => p is Opportunity).Select(p => (Opportunity)p).Take(10).ToList();

            var gbs = new GabrieleBaselineSelector();
            var baselines = gbs.GenerateBaselines(s);
            return;

            // baseline test
            /*var watch = System.Diagnostics.Stopwatch.StartNew();
            var gbs = new GabrieleBaselineSelector();
            var baselines = gbs.GenerateBaselines(s);
            watch.Stop();

            Console.WriteLine();
            Console.WriteLine($"Created a total of {baselines.Count} baselines in {watch.ElapsedMilliseconds} miliseconds");

            
            // create a result for each optimizer
            foreach (var o in optimizers)
            {
                var schedule = o.Generate(s, baselines.First());
                Verifier.Verify(schedule);
                //ToCSV(schedule);
            }*/

            Console.WriteLine("Done.");
            Console.ReadKey();
        }

        private static void ToJson(Schedule schedule)
        {
            using (var csv = File.CreateText("results.json"))
            {
                csv.Write(JsonConvert.SerializeObject(schedule, Formatting.Indented).ToString());
            }
        }
    }

    public class HtmlEncodeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString().Replace("'", "\\'"));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = JToken.Load(reader).Value<string>();
            return System.Net.WebUtility.HtmlEncode(value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }
}
