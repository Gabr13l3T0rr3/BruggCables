'''
DEPRECATED CODES PRODUCED FOR FILE BASED DATASETS.

'''



import datetime

import numpy as np
import pandas as pd

from matplotlib import pyplot as plt
from matplotlib import gridspec



def get_open_offers_df(datafile='../Data/Open_Opportunities.xlsx'):
    '''
    Returns a dataframe with the Open Opportunities dataset.
    '''
    open_opportunities = pd.read_excel(datafile)

    ## could clean and preprocess the data here

    return open_opportunities




def timelineplot(df, cmapname='RdYlBu_r', cutoff=datetime.datetime(2015,1,1)):

    point_attr = (('Liefertermin Ist', 'Profit Margin in % (DB1)' ), ('Auftragsvergabe', 'Wahrscheinlichkeit (%)'))
    line_attrs = (('Auftragsvergabe', 'Liefertermin Ist',),)

    N = len(df)

    now = datetime.datetime(2016, 1, 18)
    td = datetime.timedelta(days=90)

    # SET UP COLORMAPS
    cmaps = []
    for pattr in point_attr:
        cmaps.append((plt.get_cmap(cmapname),
                    df[pattr[1]].min(), df[pattr[1]].max()))

    # BUILD PLOT

    ratios = [30,]+len(cmaps)*[1,]
    gs = gridspec.GridSpec(1+len(cmaps), 1, height_ratios=ratios)
    fig = plt.figure(figsize=(18, 15+3*len(cmaps)))
    
    ax0 = plt.subplot(gs[0]) 

    for i in range(N):

        crit = []
        for lattr in line_attrs:
            val = df[lattr[0]][i]
            crit.append(pd.isnull(val) or val < cutoff)
            val = df[lattr[1]][i]
            crit.append(pd.isnull(val) or val < cutoff)

        for pattr in point_attr:
            val = df[pattr[0]][i]
            crit.append(pd.isnull(val))

        if any(crit): continue

        # LINES
        for lattr in line_attrs:
            _ = ax0.plot([df[lattr[0]][i], df[lattr[1]][i]], [i, i], '-', color=(0.8, 0.8, 0.8, 1.0), lw=0.2) #color=cmap(df[cmap_attr][i]/cmmax))

        # POINTS
        for pidx, pattr in enumerate(point_attr):
            val = df[pattr[0]][i]
            _ = ax0.plot([val, ], [i, ], 'o', color=cmaps[pidx][0](df[pattr[1]][i]/cmaps[pidx][2]))

    #_ = ax0.plot(df['Liefertermin Ist'].dropna(), df['Liefertermin Ist'].dropna().index, 'o')

    ## WINDOW LINES
    _ = ax0.plot([now, now], [0, N], '-', color='red')
    _ = ax0.plot([now+td, now+td], [0, N], '--', color='red')
    _ = ax0.plot([now+3*td, now+3*td], [0, N], '--', color='red')

    #_ = ax0.set_xlabel('Liefertermin Ist (o), Auftragsvergabe (-)')


    # COLORBARS
    for cmi, cm in enumerate(cmaps):
        cmgrad = np.linspace(cm[1], cm[2], 256)
        cmgrad = np.vstack((cmgrad, cmgrad))

        ax = plt.subplot(gs[1+cmi]) 
        cmapim = ax.imshow(cmgrad, cmap=cm[0], extent=[cm[1], cm[2], 0,1], aspect='auto')
        ax.get_yaxis().set_visible(False)
        ax.set_xlabel(point_attr[cmi][1])

    plt.tight_layout()

    return fig
