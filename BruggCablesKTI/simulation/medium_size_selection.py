

from datetime import datetime
from dateutil.relativedelta import relativedelta
from copy import copy
import itertools
from itertools import combinations as itercomb

from dateutil import relativedelta
import numpy as np
import pandas as pd


START_DATE = datetime(2016, 1, 1) # first day of the schedule
N_CLUSTERS = 6                   # number of cluster in the schedule

END_DATE = START_DATE + relativedelta.relativedelta(months = N_CLUSTERS)
                                  # last day of the schedule
MAX_LOAD_L1 = 24.                 # hours per day
MAX_LOAD_L2 = 24.                 # hours per day
OVER_FACTOR = 1.5                 # overload acceptance factor
MAX_VOLTAGE_L1 = 630.             # max voltage for line 1
MAX_AREA_L1 = 150. 	              # max area for line 1
sched_hours = 2 * (END_DATE - START_DATE).total_seconds() / 3600.


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
    print(df.revenue.min())
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



def first_selection_reduction(selections, attributes_list):
    '''

    '''
    #1) generate the dataframe with the opportune quantity
    attr_sel = []
    df = pd.DataFrame(attributes_list)

    for selection in selections:
        df_tmp = df[df.id.isin(selection)]
        attrs = {
            'workload':     df_tmp.workload.sum(),
            'workload_tot': df_tmp.workload_tot.sum() ,
            'margin':       df_tmp.margin.sum(),
            'revenue':      df_tmp.revenue.sum(),}
        attr_sel.append(attrs)

    df = pd.DataFrame(attr_sel)
    if len(df) > 1.e9:
        centroids, labels = first_selection_clustering(selections, df)
    else:
        centroids = df; labels = range(0,len(df))

    return centroids, labels



def first_selection_clustering(selections, df):
    '''

    '''
    from sklearn.cluster import Birch
    from sklearn import metrics
    from sklearn.datasets.samples_generator import make_blobs

    X = (df / df.std()).as_matrix()

    ################################ Birch #####################################
    birch_model = Birch(threshold=0.5, n_clusters=None)
    birch_model.fit(X)
    labels = birch_model.labels_
    centroids = birch_model.subcluster_centers_
    df_cent = pd.DataFrame( data = centroids,
                            index = range(0,len(centroids)),
                            columns = df.columns)
    n_clusters = np.unique(labels).size
    df_cent = df_cent * df.std()
    print(n_clusters, len(df))

    return df_cent, labels



def second_selection_(centroids):
    '''
    '''

    xxx, n_selc = np.unique(centroids.cluster.values, return_counts = True)
    uplimt = np.cumsum(n_selc)
    offset = uplimt - n_selc
    delta = uplimt - offset
    uplimt -= 1

    cursor = np.zeros(N_CLUSTERS, int)
    indics = np.where(delta > 1)
    mindcs = np.min(indics)

    npop = 0

    import pdb; pdb.set_trace()

    sec_sel = []

    import time
    while (cursor != delta - 1).any():
        if ((npop + 1) % 1000==0): s = time.clock()

        df = centroids[centroids.index.isin(offset+cursor)]
        sec_sel.append(df.sum().to_dict())

        ############################## update rule #############################
        cursor[mindcs] += 1
        npop += 1
        while (cursor == delta).any():
            iup = mindcs + np.min(np.where(cursor[indics] == delta[indics]))
            if iup+1 == N_CLUSTERS:
                iup = np.min(np.where(cursor != delta))
            cursor = cursor % delta
            cursor[iup+1] += 1
        ########################################################################

        if (npop % 1000==0):
            print("%8.2f"%((npop)/float(np.prod(delta))),(time.clock()-s))

    import pdb; pdb.set_trace()
    sec_sel = pd.DataFrame(sec_sel)
    sec_sel_pro = sec_sel[ \
                   (sec_sel.workload_tot >= sec_sel.workload_tot.max() * 0.75)&\
                   (sec_sel.workload_tot <= sec_sel.workload_tot.max())]

    print(len(sec_sel), len(sec_sel_pro))

    import matplotlib.pyplot as plt
    plt.scatter(sec_sel_pro.workload_tot.values, sec_sel_pro.margin.values)
    plt.xlabel('Workload (%)')
    plt.ylabel('Total Margin (CHF)')
    plt.show()
    import pdb; pdb.set_trace()
    return sec_sel_pro



def read_ids_form_df(df, centroids, first_selections, batches, dbh):

    batches = pd.DataFrame(batches)

    xxx, n_selc = np.unique(centroids.cluster.values, return_counts = True)
    limt = (np.cumprod(n_selc))[::-1]

    indices = np.zeros((len(df),N_CLUSTERS))
    ref = df.index.values

    for i in range(0,N_CLUSTERS):
        if i != N_CLUSTERS-1:
            indices[:,(N_CLUSTERS-1)-i] = np.trunc(ref/limt[i+1])
            ref = ref - indices[:,N_CLUSTERS-1-i] * limt[i+1]
        else:
            indices[:,(N_CLUSTERS-1)-i] = ref

    sched_content = []

    for sel_ind in indices:
        print(sel_ind)
        id_to_db = []

        for icl in range(0,N_CLUSTERS):
            sel_crit = (first_selections.cluster == icl).values & \
                             (first_selections.label == sel_ind[icl]).values

            id_to_db.append(first_selections[sel_crit].ids.values)

        for comb in (list(itertools.product(*id_to_db))):
            prmry_ks = [item for sublist in list(comb) for item in sublist]
            nones = [None] * len(prmry_ks)
            dbh.add_schedule(tuple(zip(prmry_ks, nones)))

            from schedule import schedule
            tmp = schedule()

    import pdb; pdb.set_trace()
    return indices



def generate_first_selections_on_database(dbfile, clustersize='month'):

    '''

    Reads from the datadase the specifics for the batches and set up a dataframe
    for the first selection step. Stores the results of the firste into the
    FirstSelection table

    Arguments
    ---------
    dbfile : string containing the path to the database file location
    clustersize : clustersize

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

    # 2. loop over clusters defined by clustersize
    list_centroids = []
    list_labels = []
    list_selections = []
    batches_repo = []

    for i_cluster in range(0, N_CLUSTERS+1):

        cl_e_date = cl_s_date + relativedelta(months = 1)
        clusterdays = (cl_e_date-cl_s_date).days
        print(cl_s_date, cl_e_date)

        attributes_list = []
        batches = dbh.select_batch_timerange(cl_s_date, cl_e_date)

        # get list of dictionaries of first batches in cluster
        count = 0
        for batch in batches:
            attributes = {
                    'id': batch.pk,
                    'workload': batch.workload,
                    'opp_kind': batch.cable.opportunity.kind,
                    'cable_kind': batch.cable.kind,
                    'area': batch.cable.area,
                    'voltage': batch.cable.voltage,
                    'line': batch.cable.production_line,
                    'id_list': [bat.pk for bat in batch.cable.batches
                                if bat.delivery_date <= END_DATE] ,
                    ### temporary attributes ###
                    'probability': batch.cable.opportunity.probability ,
                    'margin': batch.cable.opportunity.margin,
                    'revenue': batch.cable.opportunity.revenue,
                    'workload_tot': np.sum([(bat.workload/sched_hours)
                                            for bat in batch.cable.batches
                                            if bat.delivery_date <= END_DATE])
                    }

            if (attributes['opp_kind'] == 'offer') and \
                                                (attributes['revenue'] != None):
                count += 1
                if attributes['revenue'] > 1.5e6:
                    attributes_list.append(attributes)
            else:
                attributes_list.append(attributes)
        print('off_n',count)
        print(len(attributes_list), 'Opportunities within the ',
                                                        i_cluster ,'th Cluster')

        # 3. generate first selections
        selections = first_selection(attributes_list, clusterdays)

        ### new stuff - > to be finalized
        centroids, label = first_selection_reduction(selections,attributes_list)
        centroids['cluster'] = pd.Series([i_cluster]*len(centroids),
                                                        index = centroids.index)
        #### storing the centroids
        if i_cluster == 0:
            list_centroids = centroids
        else:
            list_centroids = pd.concat([list_centroids,centroids],
                                                            ignore_index = True)

        df = pd.DataFrame(attributes_list)
        for i in range(0,len(selections)):
            tmp = df[df.id.isin(selections[i])].id_list.values.tolist()
            list_selections.append({'ids':[item for sublist in tmp
                                                for item in sublist],
                                    'cluster':i_cluster,
                                    'label':label[i]})
        # 4. write to db
        #utils.write_first_selections_for_cluster(dbh, selections,
        #                                               cluster_number=i_cluster)
        batches_repo += attributes_list
        cl_s_date = copy(cl_e_date)

    sec_sel_ = second_selection_(list_centroids)
    # read_ids_form_df(sec_sel_,
    #                  list_centroids,
    #                  pd.DataFrame(list_selections),
    #                  batches_repo, dbh)

    import pdb; pdb.set_trace()



def main():
    from shutil import copyfile,rmtree

    dbfile_template = '/Users/sgi01501141/Desktop/new_model/BruggCables_new.db'
    dbfile = '/Users/sgi01501141/Desktop/new_model/BruggCables.db'

    rmtree(dbfile, ignore_errors = True)
    copyfile(dbfile_template, dbfile)

    generate_first_selections_on_database(dbfile, clustersize='month')


if __name__ == '__main__':
    main()
