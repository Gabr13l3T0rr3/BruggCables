using Optimization;
using Optimization.DataModel;
using Optimization.Optimizers;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using static Optimization.DataModel.Schedule;

namespace UI.Models
{
    public class ScheduleViewModel
    {
        public ScenarioDataContext DataContext;
        public List<Opportunity> PriorityOpportunities { get; set; }
        public List<Baseline> AllBaselines { get; set; }
        public List<Baseline> SelectedBaselines { get; set; }
        public List<FilledBaseline> FilledBaselines { get; set; }
        public Schedule CurrentSchedule { get; set; }
        public ProductionParameters Parameters { get; set; }

        public ScheduleViewModel(string basePath)
        {
            DataContext = new ScenarioDataContext(basePath);
            PriorityOpportunities = new List<Opportunity>();
            AllBaselines = new List<Baseline>();
            SelectedBaselines = new List<Baseline>();
            FilledBaselines = new List<FilledBaseline>();
            Parameters = new ProductionParameters();
        }
    }

    public class ScenarioDataContext : DbContext
    {
        private string _basePath;

        public ScenarioDataContext(string basePath)
        {
            _basePath = basePath;
        }

        private Scenario _loadedScenario;
        public Scenario Scenario {
            get
            {
                if (_loadedScenario == null)
                {
                    _loadedScenario = Scenario.Load(DateTime.MinValue, DateTime.MaxValue,
                   Path.Combine(_basePath, "Testfiles/Open_Opportunities.xlsx"),
                   Path.Combine(_basePath, "Testfiles/Lines.xlsx")); //DateTime.Parse("2/29/2016 12:00:00 AM"), DateTime.Parse("7/6/2016 12:00:00 AM")

                }
                return _loadedScenario;
            }
        }

        private Dictionary<FilledBaseline, Schedule> _fbCache = new Dictionary<FilledBaseline, Schedule>();
        public Schedule CalcSchedule(FilledBaseline fb)
        {
            if (!_fbCache.ContainsKey(fb))
            {
                var optimizer = new LPOptimizerIndescrete();
                var schedule = optimizer.Generate(Scenario, fb);
                _fbCache.Add(fb, schedule);
            }
            return _fbCache[fb];
        }
    }

    public class BatchViewModel
    {
        public BatchAllocation batch { get; set; }
        public ProjectAllocation project { get; set; }

        public BatchViewModel(ProjectAllocation p, BatchAllocation b)
        {
            batch = b;
            project = p;
        }
    }

    public class WizardViewModel
    {
        public readonly string[] Pages = new string[] {
            "PrioritySelection",
            "BaselineSelection",
            "FilledBaselines",
            "MustWinIdentification"
        };
        public int Step;
    }
}