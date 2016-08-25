
from matplotlib import pyplot as plt

from datetime import datetime, timedelta


def batches(df, cutoff=datetime(2015,1,1)):
    '''
    '''
    opp_ids = df.opportunity_id.unique()

    dfsorted = df.where(df.delivery_date > cutoff).sort_values(by='delivery_date')

    opp_offsets = { ide : offset 
            for offset, ide in enumerate(dfsorted['opportunity_id'].unique() ) }
    
    fig, ax = plt.subplots(figsize=(18, 20))
     
    for idx in range(dfsorted.shape[0]):

        batch = dfsorted.iloc[idx]
        offset = opp_offsets[batch['opportunity_id']]

        delivery_date = batch['delivery_date']
        production_start = batch['delivery_date']-timedelta(hours=batch['workload'])

        #print('batch idx:', idx, 'offset', offset, 'prod window: ', [production_start, delivery_date,])

        color = 'red' if batch.opportunity_kind == 'project' else 'black'
        ax.plot([production_start, delivery_date,],[offset, offset,], lw=5, color=color, alpha=0.5)

    ax.set_title('Timeline Plot for Offers and Projects')

    return fig


        


