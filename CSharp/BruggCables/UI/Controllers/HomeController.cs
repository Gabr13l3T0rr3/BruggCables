using Optimization.DataModel;
using Optimization.Optimizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using UI.Models;
using static Optimization.DataModel.Schedule;

namespace UI.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(Utils.GetOrSetScheduleViewModel(Session, Server));
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Schedule(string projects)
        {
            var projectNrs = projects.Split('-').Select(s => Int32.Parse(s)).ToArray();
            var svm = Utils.GetOrSetScheduleViewModel(Session, Server);
            var filledBaseline = svm.FilledBaselines.First(fb => fb.ProjectsAreEqual(projectNrs));
            svm.CurrentSchedule = svm.DataContext.CalcSchedule(filledBaseline);
            return View(svm);
        }

        public ActionResult PrioritySelection()
        {
            return View(Utils.GetOrSetScheduleViewModel(Session, Server));
        }

        [HttpPost()]
        public ActionResult PrioritySelection(FormCollection form)
        {
            var svm = Session["ScheduleViewModel"] as ScheduleViewModel;
            if (svm != null)
            {
                // if priority opportunities changed, recalculate baselines and keep only those selected which match one of the new
                var priorities = svm.DataContext.Scenario.Projects.Where(p => form.AllKeys.Contains(p.Nr.ToString())).Select(p => (Opportunity)p).ToList();
                if (!svm.PriorityOpportunities.SequenceEqual(priorities))
                {
                    svm.PriorityOpportunities = priorities;
                    // new priority offers means we have to recalculate baselines
                    var gbs = new Optimization.BaselineSelectors.GabrieleBaselineSelector();
                    var baselines = gbs.GenerateBaselines(svm.DataContext.Scenario);
                    svm.AllBaselines = baselines;
                    // only make those baselines selected which are still available
                    svm.SelectedBaselines = svm.SelectedBaselines.Where(sb => baselines.Contains(sb)).ToList();
                }
            }
            return RedirectToAction("BaselineSelection");
        }

        public ActionResult BaselineSelection()
        {
            return View(Utils.GetOrSetScheduleViewModel(Session, Server));
        }

        [HttpPost()]
        public ActionResult BaselineSelection(FormCollection form)
        {
            var svm = Session["ScheduleViewModel"] as ScheduleViewModel;
            if (svm != null)
            {
                // set the selected baselines
                var newSelectedBaselines = Enumerable.Range(0, svm.AllBaselines.Count).Where(i => form.AllKeys.Contains("baseline_" + i)).Select(i => svm.AllBaselines[i]).ToList();
                if (!svm.SelectedBaselines.SequenceEqual(newSelectedBaselines))
                {
                    svm.SelectedBaselines = newSelectedBaselines;
                    var fbl = svm.SelectedBaselines.AsParallel().SelectMany(bl => new RomansFiller().Generate(svm.DataContext.Scenario, bl, svm.Parameters)).ToList();
                    svm.FilledBaselines = fbl;
                }
            }
            return RedirectToAction(form.AllKeys.Contains("Back") ? "PrioritySelection" : "FilledBaselines");
        }

        public ActionResult FilledBaselines()
        {
            return View(Utils.GetOrSetScheduleViewModel(Session, Server));
        }

        [HttpPost()]
        public ActionResult FilledBaselines(FormCollection form)
        {
            var svm = Session["ScheduleViewModel"] as ScheduleViewModel;
            if (svm != null)
            {
            }
            return RedirectToAction(form.AllKeys.Contains("Back") ? "BaselineSelection" : "MustWinIdentification");
        }

        public ActionResult MustWinIdentification()
        {
            return View(Utils.GetOrSetScheduleViewModel(Session, Server));
        }

        [HttpPost()]
        public ActionResult MustWinIdentification(FormCollection form)
        {
            var svm = Session["ScheduleViewModel"] as ScheduleViewModel;
            if (svm != null)
            {
            }
            return RedirectToAction("FilledBaselines");
        }
    }
}