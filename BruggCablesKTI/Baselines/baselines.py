from datetime import datetime
from dateutil.relativedelta import relativedelta
from itertools import chain, combinations as itercomb
import itertools
import numpy as np
import pandas as pd
from BruggCablesKTI.db.model import DBHandler
from BruggCablesKTI.db import utils


################################
### DATABASE NAME DEFINITION ###
################################
dbfile_template = '/Users/sgi01501141/Desktop/new_model/BruggCables_new.db'
dbfile = '/Users/sgi01501141/Desktop/new_model/BruggCables.db'

####################################
### TIME PARAMETERS FOR SCHEDULE ###
####################################
START_DATE = datetime(2016, 1, 1)                                               # start scheduling time
N_CLUSTERS = 6                                                                  # number of cluster in the schedule
END_DATE = START_DATE + relativedelta(months = N_CLUSTERS)                      # end scheduling time
sched_hours = (END_DATE - START_DATE).total_seconds() / 3600.                   # HOURS WITHIN THE SELECTED TIMERANGE

#################################
### PARAMETERS FOR PRODUCTION ###
#################################
MAX_LOAD_L1    = 24.                                                            # hours per day
MAX_LOAD_L2    = 24.                                                            # hours per day
OVER_FACTOR    = 1.1                                                            # overload acceptance factor
MAX_VOLTAGE_L1 = 630.                                                           # max voltage for line 1
MAX_AREA_L1    = 150. 	                                                        # max area for line 1
MID_SIZE_LEVEL = 2e6                                                            # MIDSIZE OFFERS DEFAULT OPTION


def first_combination(btc , s_date , e_date):
    first_selections = []

    wl_base_L1 = btc[(btc.opportunity_kind != 'offer') &\
                     (btc.cable_production_line == 'Line1')].workload.sum()

    wl_base_L2 = btc[(btc.opportunity_kind != 'offer') &\
                     (btc.cable_production_line == 'Line2')].workload.sum()

    ###################################
    ### MIDDLE SIZE OFFER SELECTION ###
    ###################################
    offers = btc[(btc.opportunity_kind == 'offer') &\
                 (btc.opportunity_revenue >= MID_SIZE_LEVEL)]
    offers[offers.opportunity_revenue.notnull()]
    offers[offers.opportunity_margin.notnull()]

    if len(offers) < 1:
        return btc[(btc.opportunity_kind != 'offer')].opportunity_id.tolist()

    elif len(offers) >= 1:
        opportunity_ids = list(set(offers.opportunity_id.tolist()))
        proj_id = btc[btc.opportunity_kind != 'offer'].opportunity_id.tolist()
        days = (e_date - s_date).days

        for i in np.arange(0,len(opportunity_ids)+1):
            combinations = list(itercomb(opportunity_ids , i))

            for element in combinations:
                dfa = btc[btc.opportunity_id.isin(list(element))]

                sel_off_L1 = (dfa.cable_voltage <= MAX_VOLTAGE_L1) & \
                             (dfa.cable_area <= MAX_AREA_L1)

                sel_off_L2 = (dfa.cable_voltage > MAX_VOLTAGE_L1) | \
                             (dfa.cable_area > MAX_AREA_L1) | \
                             (dfa.cable_kind == 'SEG')

                off_L1_workload = dfa[sel_off_L1].workload.sum()
                off_L2_workload = dfa[sel_off_L2].workload.sum()

                L1_workload = off_L1_workload + wl_base_L1
                L2_workload = off_L2_workload + wl_base_L2

                if (L1_workload < days * MAX_LOAD_L1 * OVER_FACTOR) and \
                   (L2_workload < days * MAX_LOAD_L2 * OVER_FACTOR) and \
                   (L1_workload + L2_workload) < days * OVER_FACTOR \
                   * (MAX_LOAD_L1 + MAX_LOAD_L2):
                   first_selections.append(list(element) + proj_id)
    return first_selections



def second_combination(batches, ids):
    from copy import copy
    import time

    baselines = []
    n_selc = []

    for m in ids: n_selc += [len(m) if isinstance(m[0], list) else 1]

    uplimt = np.cumsum(n_selc)
    offset = uplimt - n_selc
    delta = uplimt - offset
    cursor = np.zeros(N_CLUSTERS, int)
    indics = np.where(delta > 1)[0]
    mindcs = np.min(indics)

    ##########################
    ### FIXED OPPORUNITIES ###
    ##########################
    fixd_opp = []
    fix_indics = np.where(delta == 1)[0]
    for i in fix_indics: fixd_opp += ids[i]

    npop = 0
    ######################################
    ###          PROGRESS BAR          ###
    # from ipywidgets import FloatProgress
    # from IPython.display import display
    # f = FloatProgress(min=0, max=100)
    # display(f)
    ######################################

    while (npop != np.prod(delta)):
        if ((npop + 1) % 1000==0): s = time.clock()

        #############################
        ### VARIABLE OPPORUNITIES ###
        #############################
        var_opp = []
        for i in indics: var_opp += ids[i][cursor[i]]
        curr_opp = var_opp + fixd_opp

        batchi = batches[ (batches.opportunity_id.isin(curr_opp)) & \
                          (batches.delivery_date < END_DATE) ]

        ######################
        ### WORKLOAD CHECK ###
        ######################
        if batchi.workload.sum() <= 1.1 * sched_hours * 2:
            baselines.append(curr_opp)

        ############################## update rule #############################
        cursor[mindcs] += 1
        npop += 1
        while (cursor == delta).any() and (npop != np.prod(delta)):
            iup = mindcs + np.min(np.where(cursor[indics] == delta[indics]))
            if iup+1 == N_CLUSTERS:
                iup = np.min(np.where(cursor != delta))
            cursor = cursor % delta
            cursor[iup+1] += 1
        ########################################################################

        ###########################################
        ###         PROGRESS BAR UPDATE         ###
        # f.value = int(100 * (npop / np.prod(delta)))
        ###########################################
    return baselines



def optimal_selection(dbfile):
    from BruggCablesKTI.db.pandas_utils import get_opportunities_as_df
    from BruggCablesKTI.db.pandas_utils import get_cables_as_df
    from BruggCablesKTI.db.pandas_utils import get_batches_as_df
    from copy import copy

    # 1. connect to db
    dbh = DBHandler('sqlite:///'+ dbfile)
    s_date = copy(START_DATE)

    # 1.5 generate dataframe for opportunities
    opp = get_opportunities_as_df(dbh)
    batches = get_batches_as_df(dbh)
    cables = get_cables_as_df(dbh)

    # 2. check on DELIVERY DATE
    opp[opp.delivery_date.notnull()]
    opp[opp.margin.notnull()]
    opp[opp.revenue.notnull()]

    fs_ids = []

    for imonth in range(0, N_CLUSTERS):
        e_date = s_date + relativedelta(months = 1)

        opprts_sel = opp[ (opp.delivery_date >= s_date)&\
                          (opp.delivery_date < e_date) ]

        batch_selection = (batches.number == 0) &\
                          (batches.delivery_date < e_date) &\
                          (batches.delivery_date >= s_date) &\
                          (batches.opportunity_id.isin(opprts_sel.brugg_cables_id.tolist()))

        batchs_sel = batches[batch_selection]

        # 3. monthly based combination
        fs_ids.append( first_combination( batchs_sel , s_date, e_date ) )

        s_date = copy(e_date)

    ## 4. combine the available first selections
    baselines = second_combination(batches, fs_ids)
    return baselines



def main():
    from util import compute_fitness, filling
    from shutil import copyfile,rmtree
    import time
    start = time.clock()

    ### COPY THE DATABASE TEMPLATE
    rmtree(dbfile, ignore_errors = True)
    copyfile(dbfile_template, dbfile)

    ### BASELINE GENERATION
    baselines = optimal_selection(dbfile)

    print()
    print(len(baselines), 'Baselines Generated in {0:.3f} sec.'
                                                    .format(time.clock()-start))
    print()

    ### FITNESS: compute the fitness for the baselines
    fitness = compute_fitness(baselines)

    ### SELECTION: preliminary selection rule for the best baseline
    fitness = fitness[fitness.delay == 0]
    fitness = fitness.sort_values(['workload','margin'],ascending=[False,False])
    baseline = fitness.ix[0].baselines

    ### FILLING: combine the fillers to get the schedules
    schedules = filling(baseline, START_DATE)

    fitness = compute_fitness(schedules)
    fitness.to_pickle('fitness.pkl')

    import pdb; pdb.set_trace()

if __name__ == '__main__':
    main()
