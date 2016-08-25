from datetime import datetime
from dateutil.relativedelta import relativedelta
import numpy as np
import pandas as pd

import pdb

N_CLUSTERS = 12
START_DATE = datetime(2016,1,1)
END_DATE = START_DATE + relativedelta(months = N_CLUSTERS)

MAX_LOAD_L1 = 24.                 # hours per day
MAX_LOAD_L2 = 24.                 # hours per day
SCND_LOC_RATE = 1.5
SCND_TOT_RATE = 1.1


def get_cluster_edges():

    '''
    Computes the time difference in days between the end of each cluster and the
    scheduling starting date
    Arguments
    ---------

    Returns
    -------
    cluster_edges: list
    '''

    cluster_edges = [0]
    for icl in range(1,N_CLUSTERS+1):
        cluster_edges.append(
            (START_DATE + relativedelta(months = icl) - START_DATE).days)
    return cluster_edges


def check_schedule(schedule, cluster_edges):

    '''
    Performs a set of checks on schedules to generate the secon selection
    Arguments
    ---------
    schedule: workload distrubution over the clustered schedule and generate by
                the secind selection
    cluster_edges: days per cluster

    Returns
    -------
    True if all the conditions are fullfilled else False
    '''

    cluster_days = (cluster_edges - np.roll(cluster_edges, 1))[1:]

    local_max_check = (schedule < (MAX_LOAD_L1 + MAX_LOAD_L2) * \
                                    SCND_LOC_RATE * cluster_days).any()

    global_max_check = np.sum(schedule) < SCND_TOT_RATE * \
                            np.sum((MAX_LOAD_L1 + MAX_LOAD_L2) * cluster_days)
    #TODO: ADD MORE CHECKS ON THE WORKLOAD

    return all([local_max_check, global_max_check])


def generate_second_selections_on_database(dbfile, clustersize='month'):

    '''
    Performs the second selection starting form the FirstSelection table.

    Arguments
    ---------
    bfile: string containing the path to the database file
    clustersize: clustersize choice

    Returns
    -------
    '''

    from copy import copy
    from dateutil.relativedelta import relativedelta
    from BruggCablesKTI.db.model import DBHandler

    cluster_edges = get_cluster_edges()

    #1.connect to the database
    dbh = DBHandler('sqlite:///'+ dbfile)

    #2.Cartesian product of the first selections
    fs_len = np.asarray([len(dbh.select_first_selection(icl))
                            for icl in range(0, N_CLUSTERS)])

    ss_len = np.prod(fs_len[np.where(fs_len > 0)],dtype = np.float64)
    ss_len = int(ss_len)

    ss_array = np.zeros(N_CLUSTERS)

    ss_curr = 0

    batch_dict = []
    for batch in dbh.batches:
        batch_dict.append(batch.to_dict())
    batch_df = pd.DataFrame(batch_dict)

    while ss_curr < ss_len:
        import time; check_time = time.clock()
        cl_sched = np.zeros(N_CLUSTERS)

        for icl in range(0, N_CLUSTERS):
            ### This step thakes 0.1 sec for single cluster
            frst_sel_pk = dbh.select_first_selection(icl, \
                                                element_number = ss_array[icl])

            ### This step thakes 3 sec for single cluster
            frst_sel_batches = dbh.find_first_selection(frst_sel_pk).batches

            prmry_ks = [batch.batch_pk for batch in frst_sel_batches]

            cl_sched[icl] = np.sum(batch_df[batch_df.pk.isin(prmry_ks)].workload)

        if check_schedule(cl_sched, cluster_edges):
            to_bd_list = []
            for pk in prmry_ks:
                to_bd_list.append((pk,None))
            dbh.add_schedule(to_bd_list)

        print('Second selection',ss_curr,'of',ss_len,'computed in',\
                                              time.clock()-check_time,'seconds')

        # Update rule
        ss_curr += 1
        ss_array[0] += 1

        while (ss_array == fs_len).any():
            iup = np.min(np.where(ss_array == fs_len))
            ss_array = ss_array % fs_len
            ss_array[iup+1] += 1


def main():
    dbfile = '/Users/sgi01501141/Desktop/new_model/BruggCables_new.db'
    generate_second_selections_on_database(dbfile, clustersize='month')

if __name__ == '__main__':
    main()
