from datetime import datetime
from dateutil.relativedelta import relativedelta
from copy import copy
from itertools import combinations as itercomb

from dateutil import relativedelta
import numpy as np
import pandas as pd

START_DATE = datetime(2016, 1, 1) # start scheduling time
N_CLUSTERS = 9                    # number of cluster in the schedule

END_DATE = START_DATE + relativedelta.relativedelta(months = N_CLUSTERS)
                                  # end scheduling time
MAX_LOAD_L1 = 24.                 # hours per day
MAX_LOAD_L2 = 24.                 # hours per day
OVER_FACTOR = 1.5                 # overload acceptance factor
MAX_VOLTAGE_L1 = 630.             # max voltage for line 1
MAX_AREA_L1 = 150. 	              # max area for line 1
MID_SIZE_LEVEL = 2.e6

sched_hours = 2 * (END_DATE - START_DATE).total_seconds() / 3600.


def read_batches(dbfile, baseline, small_size_offers):

    from BruggCablesKTI.db.model import DBHandler
    from BruggCablesKTI.db import utils

    dbh = DBHandler('sqlite:///'+ dbfile)

    bl_batches = []
    for ibase in baseline:
        bl_batches.append([dbh.find_batch(ibatch) for ibatch in ibase])

    so_batches = []
    for ibase in small_size_offers:
        so_batches.append([dbh.find_batch(ibatch) for ibatch in ibase])

    import csv
    myfile = open('Baselines.csv','w')
    wr = csv.writer(myfile,  delimiter=',')
    bs = []
    for ibas in bl_batches:
        aux = []
        for ibat in ibas:
            if ibat.cable.opportunity.kind == 'offer':
                aux.append(int(ibat.cable.opportunity.brugg_cables_id))
        wr.writerow(list(set(aux)))
    myfile.close()

    myfile = open('GapFillers.csv','w')
    wr = csv.writer(myfile,  delimiter=',')
    gf = []
    for igf in so_batches:
        aux = []
        for ibat in igf:
            if ibat.cable.opportunity.kind == 'offer':
                aux.append(int(ibat.cable.opportunity.brugg_cables_id))
        wr.writerow(list(set(aux)))
    myfile.close()

    return bl_batches, so_batches



def plot_baselines_prop(bls, dbfile):

    from BruggCablesKTI.db.model import DBHandler
    from BruggCablesKTI.db import utils
    dbh = DBHandler('sqlite:///'+ dbfile)

    baselines = []
    batches = []

    import time; start = time.clock()

    for bl in bls:

        df_bl = []
        brugg_cables_ids = []
        for batch in bl:
            attr = {
                    'ids':batch.pk,
                    'kind': batch.cable.opportunity.kind,
                    'description': batch.cable.opportunity.description,
                    'delivery':batch.delivery_date,
                    'revenue':batch.cable.opportunity.revenue,
                    'margin':batch.cable.opportunity.margin,
                    'workload':batch.workload,
                    'delay': None,
                    'n_opp': 0}

            if batch.cable.opportunity.kind == 'offer':
                 brugg_cables_ids.append(batch.cable.opportunity)

            batches.append(copy(attr))
            df_bl.append(attr)

            attr['ids']  = [attr['ids']]
            attr['kind'] = [attr['kind']]
            attr['description'] = [attr['description']]

            attr['workload'] /= (24.*(END_DATE - START_DATE).days * 2.)

        df_bl = pd.DataFrame(df_bl)
        delay = []

        for icl in range(0, N_CLUSTERS):
            start_date = copy(START_DATE)
            end_date = start_date + relativedelta.relativedelta(months=1)
            sel = df_bl[ (df_bl.delivery >= start_date) & \
                         (df_bl.delivery < end_date) ]
            aux_wl = sel.workload.sum() - (2 * (end_date-start_date).days * 24.)
            delay += [aux_wl] if aux_wl >= 0 else [0]
            start_date = copy(end_date)

        df_bl = df_bl.drop('delivery', 1)
        df_bl = df_bl.sum()
        df_bl['delay'] = np.sum(delay)
        df_bl['n_opp'] = len(list(set(brugg_cables_ids)))

        baselines.append(df_bl.to_dict())

        print(time.clock() - start)

    df = pd.DataFrame(baselines)
    #####################################
    ### SCATTER PLOT OF THE BASELINES ###
    #####################################
    import matplotlib.pyplot as plt

    plt.scatter(df.workload.values,df.margin.values, c = df.n_opp.values, s = 50)

    plt.title('Baselines')
    plt.xlabel('Workload (%)')
    plt.ylabel('Profit Margin (CHF)')

    # for label, x, y, z in zip(  df.n_opp.values,
    #                             df.workload.values,
    #                             df.margin.values,
    #                             df.n_opp.values):
    #     plt.annotate( label, xy = (x, y), xytext = (-10, 10),
    #         textcoords = 'offset points', ha = 'right', va = 'bottom',
    #         bbox = dict(boxstyle = 'round,pad=0.1', fc = 'white', alpha = 0.5),
    #         arrowprops = dict(arrowstyle = '->', connectionstyle = 'arc3,rad=0'))

    cbar = plt.colorbar(orientation='horizontal')
    cbar.set_label('Number of (Priority) Offers')
    plt.grid()
    plt.savefig('Baselines_plot.png')
    plt.show()


    #######################################
    ### DISTANCE MEASURE IMPLEMENTATION ###
    #######################################
    distances = []
    df_batch = pd.DataFrame(batches)
    import time; start = time.clock()

    #for index in range(len(df)):
    for index in range(0,1):

        d_4_off = []
        for internal in range(len(df)):
            set_dif = list(set(df.ix[internal].ids) - set(df.ix[index].ids)) + \
                      list(set(df.ix[index].ids) - set(df.ix[internal].ids))

            dist = 0
            for pk in set_dif:
                dist += dbh.find_batch(pk).workload

            attr = {'dist':dist,
                    'profit':df.ix[internal].margin - df.ix[index].margin,
                    'number': df.ix[internal].n_opp}

            d_4_off.append(attr)

        distances.append(pd.DataFrame(d_4_off))
        print(time.clock() - start)

        plt.scatter(distances[index].dist,
                    distances[index].profit,
                    c = distances[index].number)

        plt.xlabel('Distance (hours)')
        plt.ylabel('Profit Margin (CHF)')

        # cbar = plt.colorbar(orientation='horizontal')
        # cbar.set_label('Number of (Priority) Offers')

        plt.show()
        #import pdb; pdb.set_trace()


    #############################################
    ### HISTOGRAM FOR THE BASELINES SELECTION ###
    #############################################
    CUTOFF_PARAM = 7.4e7

    df = df[df.margin > CUTOFF_PARAM]
    print(df)
    priority = []

    ids = df.description.tolist()
    kind = df.kind.tolist()
    bg_id = df.ids.tolist()

    for index in range(0,len(ids)):
        pk = ids[index]
        kn = kind[index]

        priority.append(list(set([pk[i] for i in range(0,len(pk)) \
                                                        if kn[i] == 'offer'])))

    import collections
    counter = collections.Counter([item for sublist in priority for item in sublist])
    names = list(counter.elements())

    fig = plt.figure(figsize=(15,10))
    plt.tick_params(axis='y', which='minor', labelsize=4)
    for i in range(0,len(counter)):
        rects1 = plt.hlines(i+1, 0, list(counter.values())[i], linewidth=10,
                            colors='red', linestyle='solid')
    plt.yticks(range(1, len(set(names))+1), set(names), fontsize = 10)
    plt.ylim([0, len(set(names)) + 1])
    plt.xlim([0, np.max(list(counter.values())) + 1])
    plt.xlabel('Occurrencies')
    plt.savefig('Frequencies.png')
    plt.show()

    import pdb; pdb.set_trace()



def optimizer(dbfile, baseline, small_size_offers):

    bl, so = read_batches(dbfile, baseline, small_size_offers)

    plot_baselines_prop(bl,dbfile)

    import pdb; pdb.set_trace()



def main():

    from shutil import copyfile,rmtree
    from baseline_simulation import generate_baselines
    import time

    start = time.clock()

    dbfile_template = '/Users/sgi01501141/Desktop/new_model/BruggCables_new.db'
    dbfile = '/Users/sgi01501141/Desktop/new_model/BruggCables.db'

    rmtree(dbfile, ignore_errors = True)
    copyfile(dbfile_template, dbfile)

    baselines, small_size_offers = generate_baselines(dbfile, clustersize='month')

    optimizer(dbfile, baselines, small_size_offers)

    print( time.clock()-start )
    import pdb; pdb.set_trace()



if __name__ == '__main__':
    main()
