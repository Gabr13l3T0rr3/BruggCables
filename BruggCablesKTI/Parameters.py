__author__ = 'sgi01501141'
# Sample configurable:
import datetime
from traitlets.config.configurable import Configurable
from traitlets import (Unicode, Int, Float)
from dateutil.relativedelta import relativedelta

class Parameters(Configurable):

    name = Unicode(u'defaultname', config=True)
    
    start_time = Unicode("2016-1-1") # in format %Y-%m-%d
    n_cluster = 12
    max_load_rate = Float(1.2)
    min_load_rate = Float(0.75)
    first_sel_rate = Float(2.)
    max_week_hours_L1 = Float(7. * 24.)
    max_week_hours_L2 = Float(7. * 24.)
    max_rate_firts_sel = Float(2.)
    offer_file = Unicode("./Data/Open Opportunities.xls")
    project_file = Unicode("./Data/Lines.xlsx")

    # The rest of the class implementation would go here..

    @property
    def start_date(self):
        return datetime.datetime.strptime(self.start_time, "%Y-%m-%d")
    @property
    def end_date(self):
        return self.start_date + relativedelta(months = self.n_cluster)

    def max_cluster_hours_L1(self, start_date, end_date):
        days = (end_date - start_date).days
        return self.max_week_hours_L1 * days / 7.

    def max_cluster_hours_L2(self, start_date, end_date):
        days = (end_date - start_date).days
        return self.max_week_hours_L1 * days / 7.

### max number of hour per cluster: -> to be done


# class Parameters(object):
#     def __init__(self):
#
#         self.StartDate      = datetime.date(2016,1,1)
#         self.NCluster       = 12
#         self.FirstSelRate   = 2.
#         self.MaxLoadRate    = 1.2
#         self.MinLoadRate    = 0.75
#         self.WeekInCluster  = 4
#         self.RatioOffers    = 0.
#         self.MaxWeekHoursL1 = 7. * 24.
#         self.MaxWeekHoursL2 = 7. * 24.
#         self.NIter          = 1
#         self.EndDate = self.StartDate + relativedelta(months = self.NCluster)
#         self.MaxClusterHoursL1 = self.MaxWeekHoursL1 * self.WeekInCluster
#         self.MaxClusterHoursL2 = self.MaxWeekHoursL2 * self.WeekInCluster
#         self.OfferFile = "./Data/Open Opportunities.xls"
#         self.ProjectFile = "./Data/Lines.xlsx"
#         self.SaveFile = "./ScheduleOutfit.csv"
#
#     def __set__(self):
#         return ("StartDate", "EndDate", "NCluster", "MaxLoadRate", "MinLoadRate", "RatioOffers", "MaxWeekHoursL1", "MaxWeekHoursL2", "MaxClusterHoursL1",
#                 "MaxClusterHoursL2" ,"OfferFile", "ProjectFile", "FirstDelRate"
#                 .format(self.StartDate, self.EndDate, self.NCluster, self.MaxLoadRate, self.MinLoadRate, self.RatioOffers, self.MaxWeekHoursL1, self.MaxWeekHoursL2,
#                         self.MaxClusterHoursL1, self.MaxClusterHoursL2, self.OfferFile, self.ProjectFile, self.FirstSelRate))
#
