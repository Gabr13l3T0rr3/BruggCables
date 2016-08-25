using Optimization.Testfiles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimization.DataModel
{
    public class Scenario
    {
        public readonly Project[] Projects;

        public Scenario(Project[] projects)
        {
            Projects = projects;
        }

        public static Scenario Load(
            DateTime rangeStart,
            DateTime rangeEnd,
            string opportunitiesPath = "Testfiles/Open_Opportunities.xlsx",
            string linesPath = "Testfiles/Lines.xlsx")
        {
            // load opportunities
            var opWorksheets = ExcelTableReader.LoadWorksheets(opportunitiesPath);

            var p = new List<Project>();

            foreach (var row in opWorksheets.First().Value)
            {
                if (new [] { "Querschnitt mm?", "Jährlicher Betrag Standartwährung", "Liefertermin Ist" }.Any(s => string.IsNullOrWhiteSpace(row[s].ToString())))
                    continue;

                int nr = int.Parse(row["Nr."].ToString());
                var descr = row["Verkaufschance Bezeichnung"].ToString();
                double probability = double.NaN;
                double.TryParse(row["Wahrscheinlichkeit (%)"].ToString(), out probability);
                string phase = row["Verkaufsphase"].ToString();
                
                var lt = row["Liefertermin Ist"].ToString();
                var deliveryDate = DateTime.MaxValue;
                if (!string.IsNullOrWhiteSpace(lt))
                {
                    if (!DateTime.TryParse(lt, out deliveryDate))
                    {
                        DateTime.TryParseExact(lt, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out deliveryDate);
                    }
                }
                var batches = Batch.CalculateBatches(int.Parse(row["Menge (m)"].ToString()), int.Parse(row["Spannungsebene KV"].ToString()), int.Parse(row["Querschnitt mm?"].ToString()));
                if (!string.IsNullOrWhiteSpace(row["Menge (m) (2)"].ToString()))
                    batches = batches.Concat(Batch.CalculateBatches(int.Parse(row["Menge (m) (2)"].ToString()), int.Parse(row["Spannungsebene KV (2)"].ToString()), int.Parse(row["Querschnitt mm? (2)"].ToString()))).ToArray();
                var revenue = double.Parse(row["Jährlicher Betrag Standartwährung"].ToString().Replace("CHF", "").Replace(",", ""));
                var margin = double.Parse(row["Profit Margin (DB1) in CHF"].ToString().Replace("CHF", "").Replace(",", ""));
                if (deliveryDate.AddDays(7 * batches.Count()).AddDays(7 * 3) >= rangeStart && deliveryDate <= rangeEnd)
                    p.Add(new Opportunity(nr, descr, deliveryDate, batches, revenue, margin, probability, phase));
            }

            // load projects
            var projectWorksheets = ExcelTableReader.LoadWorksheets(linesPath);

            foreach (var row in projectWorksheets.First().Value)
            {
                int nr = int.Parse(row["Auftrag"].ToString());
                var descr = row["Bezeichnung"].ToString();
                var lt = row["Datum"].ToString(); //LT? TODO
                var deliveryDate = string.IsNullOrWhiteSpace(lt) ? DateTime.MaxValue : DateTime.Parse(lt);
                var line = int.Parse(row["Linie"].ToString().Replace("Linie", "")) == 1 ? Batch.LineCompatibility.Line1 : Batch.LineCompatibility.Line2;
                var batches = Batch.CalculateBatches(double.Parse(row["Zeit"].ToString()), line);
                var isInternal = new string[] { "Medium Voltage", "Medium Voltage for stock", "internal project" }.Contains(row["Remarks"].ToString());
                var revenueText = row["Revenue (CHF)"].ToString();
                var revenue = string.IsNullOrWhiteSpace(revenueText) || revenueText == "NA" ? 0 : double.Parse(revenueText.Replace("CHF", ""));
                var marginText = row["Margin (%)"].ToString();
                var margin = string.IsNullOrWhiteSpace(marginText) || marginText == "NA" ? 0 : double.Parse(marginText.Replace("%", ""));
                margin *= revenue / 100;
                if (deliveryDate.AddDays(7 * batches.Count()).AddDays(7 * 3) >= rangeStart && deliveryDate <= rangeEnd)
                    p.Add(new FixedProject(nr, descr, deliveryDate, batches, revenue, margin, isInternal));
            }

            // toss away all projects which have a batch in them with size 0
            p = p.Where(pro => !pro.Batches.Any(b => b.UsedWorkHours == 0)).ToList();

            return new Scenario(p.ToArray());
        }
    }
}
