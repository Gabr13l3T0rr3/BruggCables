using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace UI.Controllers
{
    public class DatasetController : Controller
    {
        // GET: Dataset
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Statistics()
        {
            return View(Utils.GetOrSetScheduleViewModel(Session, Server));
        }
    }
}