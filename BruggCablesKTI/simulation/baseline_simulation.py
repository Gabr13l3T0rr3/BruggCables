from datetime import datetime
from dateutil.relativedelta import relativedelta
from copy import copy
from itertools import combinations as itercomb
import itertools
from dateutil import relativedelta
import numpy as np
import pandas as pd

START_DATE = datetime(2016, 1, 1) # start scheduling time
N_CLUSTERS = 7                    # number of cluster in the schedule

END_DATE = START_DATE + relativedelta.relativedelta(months = N_CLUSTERS)
                                  # end scheduling time
MAX_LOAD_L1 = 24.                 # hours per day
MAX_LOAD_L2 = 24.                 # hours per day
OVER_FACTOR = 1.5                 # overload acceptance factor
MAX_VOLTAGE_L1 = 630.             # max voltage for line 1
MAX_AREA_L1 = 150. 	              # max area for line 1
MID_SIZE_LEVEL = 2.e6

sched_hours = 3 * (END_DATE - START_DATE).total_seconds() / 3600.


def first_selection(batches, clusterdays):
    '''

    Arguments
    ---------
    batches : a list of dictionaries containing first batches
                to be be produced in a given cluster
        dictionary keys are:
            'id', 'workload', 'opp_kind', 'cable_kind', 'area', 'voltage',

    clusterdays : the number of days in the given cluster


    Returns
    -------
    first_selctions : list of lists of ids

    '''

    first_selections = []

    df = pd.DataFrame(batches)
    #check on the margin value
    df = df[((df.margin.isnull()) & (df.opp_kind == 'offer')) == False]

    sel_proj = df.opp_kind.isin(['project','internal'])
    sel_proj_L1 = sel_proj & (df.line == 'Line1')
    sel_proj_L2 = sel_proj & (df.line == 'Line2')

    proj_L1_tot_workload = np.sum(df[sel_proj_L1]['workload'])
    proj_L2_tot_workload = np.sum(df[sel_proj_L2]['workload'])

    proj_id = df.id[sel_proj_L1].tolist() + df.id[sel_proj_L2].tolist()

    offers = df.id[df.opp_kind == 'offer']



    revenue = []
    margin = []

    if len(offers) == 0:
        first_selections.append(proj_id)
    else:
        for IOffers in np.arange(len(offers), 0, -1):

            combinations = list(itercomb(offers, IOffers))
            for element in combinations:

                dfa = df[df.id.isin(element)]

                sel_off_L1 = (dfa.opp_kind == 'offer') & \
                             (dfa.voltage <= MAX_VOLTAGE_L1) & \
                             (dfa.area <= MAX_AREA_L1)

                sel_off_L2 = (df.opp_kind == 'offer') & \
                             ((dfa.voltage > MAX_VOLTAGE_L1) | \
                              (dfa.area > MAX_AREA_L1 ) | \
                              (dfa.cable_kind == 'SEG'))

                off_L1_tot_workload = np.sum(dfa.workload[sel_off_L1])
                off_L2_tot_workload = np.sum(dfa.workload[sel_off_L2])

                L1_tot_workload = off_L1_tot_workload + proj_L1_tot_workload
                L2_tot_workload = off_L2_tot_workload + proj_L2_tot_workload

                if (L1_tot_workload < clusterdays * MAX_LOAD_L1 * OVER_FACTOR) and \
                   (L2_tot_workload < clusterdays * MAX_LOAD_L2 * OVER_FACTOR) and \
                   (L1_tot_workload + L2_tot_workload) < clusterdays * OVER_FACTOR \
                   * (MAX_LOAD_L1 + MAX_LOAD_L2):
                   first_selections.append(dfa['id'].tolist() + proj_id)

    return first_selections



def selection_proprties(selections, attributes_list, i_cluster, dbh):

    '''

    Arguments
    ---------

    Returns
    -------

    '''

    attr_sel = []
    df = pd.DataFrame(attributes_list)

    for selection in selections:
        df_tmp = df[df.id.isin(selection)]

        batches = []
        for pk in selection:
            opp = dbh.find_batch(pk).cable.opportunity
            for cable in opp.cables:
                batches += cable.batches

        total_workload = 0
        ids_ = []
        for batch in batches:
            if batch.delivery_date <= END_DATE:
                total_workload += batch.workload / sched_hours
                ids_.append(batch.pk)

        attrs = {'workload':     df_tmp.workload.sum(),
                 'workload_tot': total_workload ,
                 'margin':       df_tmp.margin.sum(),
                 'revenue':      df_tmp.revenue.sum(),
                 'cluster':      i_cluster,
                 'ids':          ids_,}

        attr_sel.append(attrs)

    centroids = pd.DataFrame(attr_sel)

    return centroids



def baselines_selection(centroids):

    '''

    Arguments
    ---------

    Returns
    -------

    '''

    xxx, n_selc = np.unique(centroids.cluster.values, return_counts = True)
    uplimt = np.cumsum(n_selc)
    offset = uplimt - n_selc
    delta = uplimt - offset

    cursor = np.zeros(N_CLUSTERS, int)
    indics = np.where(delta > 1)[0]
    mindcs = np.min(indics)

    npop = 0

    sec_sel = []

    import time
    while (npop != np.prod(delta)):

        if ((npop + 1) % 1000==0): s = time.clock()

        df = centroids[centroids.index.isin(offset+cursor)]

        sec_sel.append(
                {'ids':[item for sublist in df.ids.tolist() for item in sublist],
                'margin':df.margin.sum(),
                'revenue':df.revenue.sum(),
                'workload':df.workload.sum(),
                'workload_tot':df.workload_tot.sum(),})

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

        if (npop % 1000==0):
            print("%8.2f"%((npop)/float(np.prod(delta))),(time.clock()-s))

    sec_sel = pd.DataFrame(sec_sel)
    sec_sel = sec_sel[ sec_sel.workload_tot <= 1.1 ]

    return sec_sel.ids.tolist()



def generate_baselines(dbfile, clustersize='month'):

    '''

    Arguments
    ---------

    Returns
    -------

    '''

    import numpy as np
    from copy import copy
    from dateutil.relativedelta import relativedelta
    from BruggCablesKTI.db.model import DBHandler
    from BruggCablesKTI.db import utils

    # 1. connect to db
    dbh = DBHandler('sqlite:///'+ dbfile)
    cl_s_date = copy(START_DATE)

    small_size_offers = []

    # 2. loop over clusters defined by clustersize
    for i_cluster in range(0, N_CLUSTERS):

        cl_e_date = cl_s_date + relativedelta(months = 1)
        clusterdays = (cl_e_date-cl_s_date).days

        print(cl_s_date.date(), ' - ', cl_e_date.date())

        attributes_list = []
        batches = dbh.select_batch_timerange(cl_s_date, cl_e_date, batch_number=0)
        cl_s_date = copy(cl_e_date) #-> update the time interval

        # get list of dictionaries of first batches in cluster
        list_opp = []
        for batch in batches:
            if batch.number == 0:
                attributes = {
                        'id': batch.pk,
                        'opp_id':batch.cable.opportunity.pk,
                        'workload': batch.workload,
                        'opp_kind': batch.cable.opportunity.kind,
                        'cable_kind': batch.cable.kind,
                        'area': batch.cable.area,
                        'voltage': batch.cable.voltage,
                        'line': batch.cable.production_line,
                        # 'id_list': [bat.pk for bat in batch.cable.batches
                        #             if bat.delivery_date <= END_DATE],
                        # ### temporary attributes ###
                        'margin': batch.cable.opportunity.margin,
                        'revenue': batch.cable.opportunity.revenue,
                        # 'workload_tot': np.sum([(bat.workload)
                        #                         for bat in batch.cable.batches
                        #                         if bat.delivery_date <= END_DATE])
                        }
                if attributes['opp_id'] not in list_opp:
                    list_opp.append(attributes['opp_id'])
                    if (attributes['opp_kind'] == 'offer') and \
                       (attributes['revenue'] not in [np.nan , None]) and \
                       (attributes['margin'] not in [np.nan , None]):

                        if attributes['revenue'] >= MID_SIZE_LEVEL:
                            attributes_list.append(attributes)
                        else:
                            small_size_offers.append([bat.pk
                                                for bat in batch.cable.batches
                                                if bat.delivery_date <= END_DATE])

                    if (attributes['opp_kind'] != 'offer'):
                        attributes_list.append(attributes)


        #######################################################################
        print('Cluster:', i_cluster + 1)
        print('Number of opportunities:', len(batches))
        df = pd.DataFrame(attributes_list)
        print('Number of Projects:', len(df[(df.opp_kind != 'offer')  & \
                                            (df.revenue.isnull() == False)]))
        print('Number of Mid Off.:', len(df[(df.opp_kind == 'offer') & \
                                            (df.revenue.isnull() == False)]))
        print('Number of Small Off.:',len(small_size_offers))
        print()
        #######################################################################

        # 3. Combine the selections
        selections = first_selection(attributes_list, clusterdays)
        # 4. Read and store selection properties
        optimal = selection_proprties(selections,attributes_list,i_cluster,dbh)

        if i_cluster == 0:
            list_optimal = optimal
        else:
            list_optimal = pd.concat([list_optimal,optimal],ignore_index = True)

    baselines_ids = baselines_selection(list_optimal)

    return baselines_ids, small_size_offers



def main():
    from shutil import copyfile,rmtree

    import time
    start = time.clock()

    dbfile_template = '/Users/sgi01501141/Desktop/new_model/BruggCables_new.db'
    dbfile = '/Users/sgi01501141/Desktop/new_model/BruggCables.db'

    rmtree(dbfile, ignore_errors = True)
    copyfile(dbfile_template, dbfile)

    baselines , small_size_offers = generate_baselines(dbfile, clustersize='month')

    print( time.clock()-start )
    import pdb; pdb.set_trace()



if __name__ == '__main__':
    main()
