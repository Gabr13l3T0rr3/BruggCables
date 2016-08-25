from itertools import chain, combinations as itercomb
from dateutil.relativedelta import relativedelta
from BruggCablesKTI.db.model import DBHandler
from BruggCablesKTI.db import utils
from datetime import datetime
from copy import copy
import pandas as pd
import numpy as np

class KTIBruggCables:

    def __init__(self):
        ################################
        ### DATABASE NAME DEFINITION ###
        ################################
        self.dbfile = '/Users/sgi01501141/Desktop/new_model/BruggCables.db'

        ####################################
        ### TIME PARAMETERS FOR SCHEDULE ###
        ####################################
        self.start_date = datetime(2016, 1, 1)                                  # start scheduling time
        self.N_CLUSTERS = 6                                                     # number of cluster in the schedule
        self.end_date = \
                    self.start_date + relativedelta(months = self.N_CLUSTERS)   # end scheduling time
        self.sched_hours = \
                 (self.end_date - self.start_date).total_seconds() / 3600.      # HOURS WITHIN THE SELECTED TIMERANGE

        #################################
        ### PARAMETERS FOR PRODUCTION ###
        #################################
        self.MAX_LOAD_L1    = 24.                                               # hours per day
        self.MAX_LOAD_L2    = 24.                                               # hours per day
        self.OVER_FACTOR    = 1.2                                               # overload acceptance factor
        self.MAX_VOLTAGE_L1 = 630.                                              # max voltage for line 1
        self.MAX_AREA_L1    = 150. 	                                            # max area for line 1
        self.MID_SIZE_LEVEL = 2e6                                               # MIDSIZE OFFERS DEFAULT OPTION


    def get_opportunities(self):
        from BruggCablesKTI.db.pandas_utils import get_opportunities_as_df

        dbh = DBHandler('sqlite:///'+ self.dbfile)

        return get_opportunities_as_df(dbh)



    def get_batches(self):
        from BruggCablesKTI.db.pandas_utils import get_batches_as_df

        dbh = DBHandler('sqlite:///'+ self.dbfile)

        return get_batches_as_df(dbh)



    def compute_delay(self, baseline, batches):
        import calendar

        #1. select the interested batches
        sel_bat = batches[batches.opportunity_id.isin(baseline)]

        l1sel = (sel_bat.cable_production_line == 'Line1') | \
                ((sel_bat.cable_voltage <= self.MAX_VOLTAGE_L1) & \
                 (sel_bat.cable_area <= self.MAX_AREA_L1))

        l2sel = (sel_bat.cable_production_line == 'Line2') | \
                ((sel_bat.cable_voltage > self.MAX_VOLTAGE_L1) | \
                 (sel_bat.cable_area > self.MAX_AREA_L1) | \
                 (sel_bat.cable_kind == 'SEG'))

        sel_bat_L1 = sel_bat[l1sel]
        sel_bat_L2 = sel_bat[l2sel]

        #2. find the first production batches
        d = sel_bat.delivery_date.min()

        start_date = datetime(d.year, d.month, 1)
        self.end_date = sel_bat.delivery_date.max()

        curr_date = start_date + relativedelta(months = 1)

        L1_workload = []
        L2_workload = []
        calendar = []

        while start_date < self.end_date:

            l1batch = sel_bat_L1[(sel_bat_L1.delivery_date >= start_date) & \
                                 (sel_bat_L1.delivery_date < curr_date)]

            l2batch = sel_bat_L2[(sel_bat_L2.delivery_date >= start_date) & \
                                 (sel_bat_L2.delivery_date < curr_date)]

            L1_workload.append(l1batch.workload.sum())
            L2_workload.append(l2batch.workload.sum())

            calendar.append((curr_date - start_date).days * 24.)

            start_date = start_date + relativedelta(months = 1)
            curr_date = curr_date + relativedelta(months = 1)

        L1_workload = np.asarray(L1_workload)
        L2_workload = np.asarray(L2_workload)
        calendar = np.asarray(calendar)

        overload = np.sum((L1_workload - calendar).clip(min = 0)+\
                          (L2_workload - calendar).clip(min = 0))

        return overload



    def compute_fitness(self, baselines, opportunities = 0, batches = 0):

        if isinstance(opportunities, int):
                opportunities = self.get_opportunities()
        if isinstance(batches, int):
                batches = self.get_batches()

        plt_attr = []

        for baseline in baselines:
            sel_opp = opportunities[opportunities.brugg_cables_id.isin(baseline)]
            attr = { 'workload': sel_opp.workload.sum(),
                     'revenue': sel_opp.revenue.sum(),
                     'margin': sel_opp.margin.sum(),
                     'number': len(sel_opp[sel_opp.kind == 'offer']),
                     'baselines': baseline,
                     'delay': self.compute_delay(baseline, batches), }
            plt_attr.append(attr)

        return pd.DataFrame(plt_attr)



    def preliminary_schedule(self, selections, batches = 0):

        if isinstance(batches, int): batches = self.get_batches()

        if isinstance(selections, list) and len(selections) != 0:
            if isinstance(selections[0], list) == False:
                selections = [selections]

        for selection in selections:
            s_date = copy(self.start_date)
            sel_batch = batches[batches.opportunity_id.isin(selection)]

            dict_sel = []
            dict_sel_l1 = []
            dict_sel_l2 = []

            ### ITERATIVELY SELECT BATCHES AND ALLOCATE THE PRODUCTION LOAD
            count = 0
            month = 0

            while count < len(sel_batch):
                e_date = s_date + relativedelta(months = 1)

                selection = (sel_batch.delivery_date >= s_date)  & \
                            (sel_batch.delivery_date <  e_date)

                curr_batchs = sel_batch[selection]
                count += len(curr_batchs)

                ########################
                ### LINE 1 SELECTION ###
                ########################
                l1sel = (curr_batchs.cable_production_line == 'Line1') | \
                        ((curr_batchs.cable_voltage <= self.MAX_VOLTAGE_L1) & \
                         (curr_batchs.cable_area <= self.MAX_AREA_L1))

                l1batch = curr_batchs[l1sel]
                attr_l1 = { 'workload':l1batch.workload.sum(),
                            'margin'  :l1batch.opportunity_margin.sum(),        # NORMALIZE THE REVENUE
                            'revenue' :l1batch.opportunity_revenue.sum(),       # NORMALIZE THE MARGIN
                            'number'  :len(l1batch),
                            'start_date':s_date,
                            'end_date':e_date,
                            'delay':(l1batch.workload.sum() - \
                                                    24*(e_date-s_date).days),
                            'month': month, }
                dict_sel_l1.append(attr_l1)

                ########################
                ### LINE 2 SELECTION ###
                ########################
                l2sel = (curr_batchs.cable_production_line == 'Line2') | \
                        ((curr_batchs.cable_voltage > self.MAX_VOLTAGE_L1) | \
                         (curr_batchs.cable_area > self.MAX_AREA_L1) | \
                         (curr_batchs.cable_kind == 'SEG'))

                l2batch = curr_batchs[l2sel]
                attr_l2 = { 'workload':l2batch.workload.sum(),
                            'margin'  :l2batch.opportunity_margin.sum(),        # NORMALIZE THE REVENUE
                            'revenue' :l2batch.opportunity_revenue.sum(),       # NORMALIZE THE MARGIN
                            'number'  :len(l2batch),
                            'start_date':s_date,
                            'end_date':e_date,
                            'delay':(l2batch.workload.sum() - \
                                                    24*(e_date-s_date).days),
                            'month': month, }
                dict_sel_l2.append(attr_l2)

                month += 1

                s_date = copy(e_date)

        return dict_sel_l1, dict_sel_l2



    def filling(self,baseline):

        batches = self.get_batches()

        line_1, line_2 = self.preliminary_schedule(baseline, batches = batches)
        line_1 = pd.DataFrame(line_1) ; line_2 = pd.DataFrame(line_2)

        init_start = min(line_1.start_date.tolist()+line_2.start_date.tolist())

        init_end   = max(line_1.end_date.tolist()+line_2.end_date.tolist())

        n_months = diff_month(init_end,init_start)
        start = copy(init_start)

#        for i_month in range(0,n_months):
        for i_month in range(0,self.N_CLUSTERS):

            # 1. initialize new step
            baseline_update = []
            end = start + relativedelta(months = 1)

            # 2. fillers selections for month i_month
            select = (batches.opportunity_revenue <= self.MID_SIZE_LEVEL) & \
                     (batches.opportunity_kind == 'offer') & \
                     (batches.delivery_date >= start) & \
                     (batches.delivery_date < end) & \
                     (batches.number == 0)

            batch = batches[select]

            print('Number of Fillers:',len(set(batch.opportunity_id.tolist())))

            start = copy(end)

            # 3. loop over the baselines
            if isinstance(baseline[0],list) is False: baseline = [baseline]

            for bsl in baseline:
                baseline_update.append(bsl)

                line_1, line_2 = self.preliminary_schedule(bsl, batches=batches)
                line_1 = pd.DataFrame(line_1) ; line_2 = pd.DataFrame(line_2)

                al1 = line_1[line_1.month == i_month].delay.values
                al2 = line_2[line_2.month == i_month].delay.values

                avail_time = 0
                if al1 < 0: avail_time += abs(al1)
                if al2 < 0: avail_time += abs(al2)

                # 3.5 list for teh primary key
                ids = list(set(batch.opportunity_id.tolist()))

                # 4. combinations -> start from zero
                if len(ids) <= 1:
                    baseline_update.append(bsl + ids)
                else:
                    wkl = []
                    for i in ids:
                        wkl.append(batch[batch.opportunity_id==i].workload.sum())
                    wkl = np.cumsum(np.sort(wkl))

                    up_bound = np.sum(wkl < avail_time) + 1
                    lo_bound = 0 if len(ids) - 1 < 0 else len(ids) - 1

                    for i in range(lo_bound,up_bound):
                        comb_ids = list(itercomb(ids, i))
                        for el in comb_ids:
                            btc = batch[batch.opportunity_id.isin(el)]

                            btc_L1 = btc[(btc.cable_voltage <= \
                                         self.MAX_VOLTAGE_L1)&\
                                        (btc.cable_area <= self.MAX_AREA_L1)]
                            btc_L2 = btc[(btc.cable_voltage > \
                                         self.MAX_VOLTAGE_L1)|\
                                         (btc.cable_area > self.MAX_AREA_L1)|\
                                         (btc.cable_kind == 'SEG')]

                            if btc_L1.workload.sum() <= \
                                abs(line_1[line_1.month == i_month].delay.values)\
                                                    * self.OVER_FACTOR:
                                baseline_update.append(bsl +
                                                btc_L1.opportunity_id.tolist())

                            if btc_L2.workload.sum() <= \
                                abs(line_2[line_2.month == i_month].delay.values)\
                                                    * self.OVER_FACTOR:
                                baseline_update.append(bsl +
                                                btc_L2.opportunity_id.tolist())

            baseline_update = [list(x) for x in set(tuple(x) \
                                       for x in baseline_update)]

            baseline = copy(baseline_update)

            #TODO: iterative selection on the low workload combination
        if isinstance(baseline[0],list) is not True: baseline = [baseline]

        return baseline



    def first_combination(self, btc , s_date , e_date):

        first_selections = []

        wl_base_L1 = btc[(btc.opportunity_kind != 'offer') &\
                         (btc.cable_production_line == 'Line1')].workload.sum()

        wl_base_L2 = btc[(btc.opportunity_kind != 'offer') &\
                         (btc.cable_production_line == 'Line2')].workload.sum()

        ###################################
        ### MIDDLE SIZE OFFER SELECTION ###
        ###################################
        offers = btc[(btc.opportunity_kind == 'offer') &\
                     (btc.opportunity_revenue >= self.MID_SIZE_LEVEL)]
        offers[offers.opportunity_revenue.notnull()]
        offers[offers.opportunity_margin.notnull()]

        if len(offers) < 1:
            return btc[(btc.opportunity_kind!='offer')].opportunity_id.tolist()

        elif len(offers) >= 1:
            opportunity_ids = list(set(offers.opportunity_id.tolist()))
            proj_id = btc[btc.opportunity_kind!='offer'].opportunity_id.tolist()
            days = (e_date - s_date).days

            for i in np.arange(0,len(opportunity_ids)+1):
                combinations = list(itercomb(opportunity_ids , i))

                for element in combinations:
                    dfa = btc[btc.opportunity_id.isin(list(element))]

                    sel_off_L1 = (dfa.cable_voltage <= self.MAX_VOLTAGE_L1) & \
                                 (dfa.cable_area <= self.MAX_AREA_L1)

                    sel_off_L2 = (dfa.cable_voltage > self.MAX_VOLTAGE_L1) | \
                                 (dfa.cable_area > self.MAX_AREA_L1) | \
                                 (dfa.cable_kind == 'SEG')

                    off_L1_workload = dfa[sel_off_L1].workload.sum()
                    off_L2_workload = dfa[sel_off_L2].workload.sum()

                    L1_workload = off_L1_workload + wl_base_L1
                    L2_workload = off_L2_workload + wl_base_L2

                    if (L1_workload < \
                            days * self.MAX_LOAD_L1 * self.OVER_FACTOR) and \
                       (L2_workload < \
                            days * self.MAX_LOAD_L2 * self.OVER_FACTOR) and \
                       (L1_workload + L2_workload) < \
                                days * self.OVER_FACTOR * \
                                (self.MAX_LOAD_L1 + self.MAX_LOAD_L2):

                       first_selections.append(list(element) + proj_id)

        return first_selections



    def second_combination(self, batches, ids):
        import time

        baselines = []
        n_selc = []

        for m in ids: n_selc += [len(m) if isinstance(m[0], list) else 1]

        uplimt = np.cumsum(n_selc)
        offset = uplimt - n_selc
        delta = uplimt - offset
        cursor = np.zeros(self.N_CLUSTERS, int)
        indics = np.where(delta > 1)[0]
        mindcs = np.min(indics)

        ### FIXED OPPORUNITIES
        fixd_opp = []
        fix_indics = np.where(delta == 1)[0]
        for i in fix_indics: fixd_opp += ids[i]

        npop = 0
        while (npop != np.prod(delta)):
            if ((npop + 1) % 1000==0): s = time.clock()

            ### VARIABLE OPPORUNITIES
            var_opp = []
            for i in indics: var_opp += ids[i][cursor[i]]
            curr_opp = var_opp + fixd_opp

            batchi = batches[ (batches.opportunity_id.isin(curr_opp)) & \
                              (batches.delivery_date < self.end_date) ]

            ### WORKLOAD CHECK
            if batchi.workload.sum() <= self.OVER_FACTOR * self.sched_hours * 2:
                baselines.append(curr_opp)

            ### UPDATE RULE
            cursor[mindcs] += 1
            npop += 1
            while (cursor == delta).any() and (npop != np.prod(delta)):
                iup = mindcs + np.min(np.where(cursor[indics] == delta[indics]))
                if iup+1 == self.N_CLUSTERS:
                    iup = np.min(np.where(cursor != delta))
                cursor = cursor % delta
                cursor[iup+1] += 1

        return baselines



    def get_baselines(self):
        from BruggCablesKTI.db.pandas_utils import get_opportunities_as_df
        from BruggCablesKTI.db.pandas_utils import get_cables_as_df
        from BruggCablesKTI.db.pandas_utils import get_batches_as_df

        # 1. connect to db
        dbh = DBHandler('sqlite:///'+ self.dbfile)
        s_date = copy(self.start_date)

        # 1.5 generate dataframe for opportunities
        opp = get_opportunities_as_df(dbh)
        batches = get_batches_as_df(dbh)
        cables = get_cables_as_df(dbh)

        # 2. check on DELIVERY DATE
        opp[opp.delivery_date.notnull()]
        opp[opp.margin.notnull()]
        opp[opp.revenue.notnull()]

        fs_ids = []

        for imonth in range(0, self.N_CLUSTERS):

            e_date = s_date + relativedelta(months = 1)

            opprts_sel = opp[(opp.delivery_date >= s_date) &\
                             (opp.delivery_date < e_date)]

            batch_selection = (batches.number == 0) &\
                              (batches.delivery_date < e_date) &\
                              (batches.delivery_date >= s_date) &\
                              (batches.opportunity_id.isin(\
                                        opprts_sel.brugg_cables_id.tolist()) )

            batchs_sel = batches[batch_selection]

            # 3. monthly based combination
            fs_ids.append(self.first_combination( batchs_sel , s_date, e_date ))
            s_date = copy(e_date)

        # 4. combine the available first selections
        baselines = self.second_combination(batches, fs_ids)
        return baselines



def main():

    import time

    start = time.clock()

    bc = KTIBruggCables()

    ### BASELINE GENERATION
    opportunities = bc.get_opportunities()
    batches = bc.get_batches()

    baselines = bc.get_baselines()

    print(len(baselines), 'Baselines Generated in {0:.3f} sec.\n'
                                                    .format(time.clock()-start))

    ### FITNESS: compute the fitness for the baselines
    fitness = bc.compute_fitness( baselines,
                                  opportunities=opportunities,
                                  batches = batches )

    ### SELECTION: preliminary selection rule for the best baseline
    fitness = fitness[fitness.delay == 0]
    fitness = fitness.sort_values(['workload','margin'],ascending=[False,False])
    baseline = fitness.iloc[0:10].baselines

    # from baselines_plot import scatter_plot
    # baselines = scatter_plot(baseline, 'workload', 'margin', df = fitness)


    # from baselines_plot import frequency_plot
    # frequency_plot(baselines.baselines.tolist(),opportunities = opportunities)

    ### FILLING: combine the fillers to get the schedules
    start = time.clock()

    schedules = []

    for ifil in range(0,len(baseline)):
        sched = bc.filling(baseline.iloc[ifil])
        schedules += sched
        print(len(sched), 'Schedules Filled in {0:.3f} sec.\n'
                                                    .format(time.clock()-start))
    import pdb; pdb.set_trace()
    ### FITNESS: compute the fitness for the baselines
    start = time.clock()
    fitness = bc.compute_fitness( schedules,
                                  opportunities=opportunities,
                                  batches = batches )

    print('Fitness Computed in {0:.3f} sec.\n'.format(time.clock()-start))

    from baselines_plot import scatter_plot
    scatter_plot(schedules, 'workload', 'margin', df = fitness)

    from baselines_plot import frequency_plot
    frequency_plot(schedules,opportunities = opportunities)

    sel_fit = copy(fitness)
    sel_fit = sel_fit.sort_values(['workload', 'margin'],ascending=[False,False])
    sel_sched = sel_fit.iloc[0].baselines

    from baselines_plot import schedule
    schedule(sel_sched)

    from baselines_plot import time_table
    time_table(sel_sched, batches = batches)

    import pdb; pdb.set_trace()


def diff_month(d1, d2):
    from datetime import datetime
    return (d1.year - d2.year)*12 + d1.month - d2.month


if __name__ == '__main__':
    main()
