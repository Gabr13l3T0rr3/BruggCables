from util import KTIBruggCables
import matplotlib.pyplot as plt

bc = KTIBruggCables()
Baselines_selection = []


def pick_elements(event):
    Baselines_selection.append(event.ind.tolist()[0])

def scatter_plot(selections, propA, propB, df = 0):
    import pandas as pd
    import numpy as np
    import matplotlib.pyplot as plt


    if isinstance(df,int): df = bc.compute_fitness(selections)

    fig = plt.figure(figsize=(8, 14))
    ax1 = plt.subplot(211)
    plt.scatter( df[propA] , df[propB], c = df['number'] , s = 50, picker=True)
    plt.xlabel(propA)
    plt.ylabel(propB)
    cbar = plt.colorbar(orientation='horizontal')
    cbar.set_label('Number of Offers')
    fig.canvas.mpl_connect('pick_event', pick_elements)
    plt.grid()

    ax2 = plt.subplot(212)
    plt.scatter( df[propA] , df[propB] , c = df['delay'] , s = 50, picker=True)
    plt.xlabel(propA)
    plt.ylabel(propB)
    cbar = plt.colorbar(orientation='horizontal')
    cbar.set_label('Delay (hours)')
    fig.canvas.mpl_connect('pick_event', pick_elements)
    plt.grid()
    plt.show()

    selec = df.iloc[list(set(Baselines_selection))]

    return selec



def frequency_plot(selections, opportunities = 0):
    if isinstance(opportunities,int):opportunities = bc.get_opportunities()

    sel_for_plot = []
    for selection in selections:
        sel_opp = opportunities[opportunities.brugg_cables_id.isin(selection)]
        sel_opp = sel_opp[sel_opp.kind == 'offer']
        sel_for_plot += (sel_opp.description.tolist())

    elements = list(set(sel_for_plot))
    frequency = [sel_for_plot.count(x) for x in set(elements)]

    for_plot = list(zip(elements , frequency))
    import pdb; pdb.set_trace()
    (elements,frequency) = for_plot.sorted(for_plot, key=lambda tup: tup[1],\
                                                                reverse = False)

    plt.figure(figsize=(16, 12))
    for i in range(0,len(frequency)):plt.hlines(i+1,0,frequency[i],linewidth=10,\
                                                colors='red',linestyle='solid')

    plt.yticks(range(1, len(elements)+1), elements, fontsize = 10)
    plt.ylim([0, len(elements) + 1])
    plt.xlim([0, max(frequency) + 5])
    plt.xlabel('Occurrencies')

    plt.show()



def schedule(selections):
    from dateutil.relativedelta import relativedelta
    from copy import copy
    import pandas as pd
    from matplotlib.dates import date2num

    batches = bc.get_batches()
    dict_sel_l1, dict_sel_l2 = bc.preliminary_schedule(selections, batches)

    plt.figure(figsize=(16, 12))
    ax = plt.subplot(211)

    ### first plot ###
    df = pd.DataFrame(dict_sel_l1)
    ax.bar(date2num(df.start_date.tolist())-7, df.workload, width=15, color='r',
            alpha = 0.3, align='center')

    df = pd.DataFrame(dict_sel_l2)
    ax.bar(date2num(df.start_date.tolist())+7, df.workload, width=15, color='g',
            alpha = 0.3, align='center')

    plt.axhline(y=24 * 30, xmin=0, xmax=1, hold=None)
    ax.xaxis_date()

    ax = plt.subplot(212)

    ### second plot ###
    df = pd.DataFrame(dict_sel_l1)

    ax.bar(date2num(df.start_date.tolist())-7, df.number, width=15, color='r',
            alpha = 0.3, align='center')

    df = pd.DataFrame(dict_sel_l2)
    ax.bar(date2num(df.start_date.tolist())+7, df.number, width=15, color='g',
            alpha = 0.3, align='center')

    ax.xaxis_date()

    plt.show()



def plot_timetable(sel_L,plt,ax):
    from dateutil.relativedelta import relativedelta
    from datetime import timedelta, date
    import matplotlib.dates as dt
    from copy import copy
    import pandas as pd
    import numpy as np

    sel_L.start = pd.Series(np.zeros(len(sel_L.index)), index=sel_L.index)
    sel_L.end = pd.Series(np.zeros(len(sel_L.index)), index=sel_L.index)

    start_date = copy(bc.start_date)
    counter = 0 ; opp_descript = []

    max_del_date = sel_L.delivery_date.max()

    # n_months = (max_del_date.year - bc.start_date.year)*12 + \
    #             max_del_date.month - bc.start_date.month
    #
    # for icl in range(0, n_months):
    for icl in range(0, bc.N_CLUSTERS):
        end_date = start_date + relativedelta(months = 1)
        select = (sel_L.delivery_date >= start_date) & \
                 (sel_L.delivery_date < end_date)
        sel_cl = sel_L[select]

        sel_cl = sel_cl.sort_values(['opportunity_revenue', 'workload'], \
                                    ascending = [False,False])
        index = sel_cl.index

        for ib in range(0, len(sel_cl)):
            sel_cl.loc[index[ib],'start'] = start_date if ib == 0 else \
                                                    sel_cl.loc[index[ib-1],'end']
            sel_cl.loc[index[ib],'end'] = sel_cl.iloc[ib].start + \
                                timedelta(hours=sel_cl.loc[index[ib],'workload'])

            color = 'b' if sel_cl.loc[index[ib],'opportunity_kind']=='offer' else 'r'
            ax = plt.hlines(counter,
                             dt.date2num(sel_cl.loc[index[ib],'start']),
                             dt.date2num(sel_cl.loc[index[ib],'end']),
                             color = color, linewidth=5.)
            counter += 1
            opp_descript.append(sel_cl.loc[index[ib],'opportunity_description'])
        start_date = copy(end_date)

    plt.yticks(np.arange(0,counter),opp_descript)
    plt.grid()



def time_table(selections, batches = 0):
    from dateutil.relativedelta import relativedelta
    from datetime import timedelta, date
    from copy import copy

    if isinstance(batches, int): batches = bc.get_batches()
    selec = copy(batches[batches.opportunity_id.isin(selections)])

    fig = plt.figure(figsize=(15, 15))

    ### PLOT LINE 1 SCHEDULE
    ax1 = plt.subplot(2,1,1, title = 'Line 1')
    ax1 = ax1.xaxis_date()

    l1sel = (selec.cable_production_line == 'Line1') | \
            ((selec.cable_voltage <= bc.MAX_VOLTAGE_L1) & \
             (selec.cable_area <= bc.MAX_AREA_L1))

    plot_timetable(selec[l1sel],plt,ax1)


    ### PLOT LINE 2 SCHEDULE
    ax2 = plt.subplot(2,1,2, title = 'Line 2')
    ax2 = ax2.xaxis_date()

    l2sel = (selec.cable_production_line == 'Line2') | \
            ((selec.cable_voltage > bc.MAX_VOLTAGE_L1) | \
             (selec.cable_area > bc.MAX_AREA_L1) | \
             (selec.cable_kind == 'SEG'))

    plot_timetable(selec[l2sel],plt,ax2)

    plt.show()
