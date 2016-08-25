import pandas as pd
import numpy as np
import math
from datetime import datetime
from dateutil.relativedelta import relativedelta

DBFILE = '/Users/sgi01501141/Desktop/new_model/BruggCables.db'
START_DATE = datetime(2016,1,1)
N_CLUSTERS = 6                   # number of cluster in the schedule
WORKING_HOURS_PER_DAY_L1 = 24.
WORKING_HOURS_PER_DAY_L2 = 24.
END_DATE = START_DATE + relativedelta(months = N_CLUSTERS)

DAYS_PER_WEEK = 7

DEL_WINDOW_PROJECT = 7      #days
DEL_WINDOW_OFFER = 7 * 4    #days

MAX_VOLTAGE_L1 = 630.             # max voltage for line 1
MAX_AREA_L1 = 150. 	              # max area for line 1


def potential_shape(opportunity, working_hours_per_day):
    '''
    '''
    if opportunity.kind in ['project','internal']:
        del_window = DEL_WINDOW_PROJECT
    elif opportunity.kind in ['offer','batch']:
        del_window = DEL_WINDOW_OFFER

    potential = init_potential(working_hours_per_day)

    days = (END_DATE - START_DATE).days
    hours = np.arange(days * working_hours_per_day)

    delivery_in_days = (opportunity.delivery_date - START_DATE).days

    end_del_range = delivery_in_days * working_hours_per_day
    start_del_range = end_del_range - (del_window * working_hours_per_day)

    in_time_ind = np.arange(start_del_range,end_del_range + 1, dtype = np.int32)

    storage_ind = np.where(hours <  start_del_range)[0]
    delay_ind = np.where(hours > end_del_range)[0]

    storage_week = start_del_range - storage_ind
    storage_week /= working_hours_per_day * DAYS_PER_WEEK
    storage_week = np.floor(storage_week) + 1

    delay_week = delay_ind - end_del_range
    delay_week /= working_hours_per_day * DAYS_PER_WEEK
    delay_week = np.floor(delay_week)


    #TODO: define in a better way potential for interanl an zero revenue proj
    if opportunity.kind is 'internal' or math.isnan(opportunity.revenue):
        potential[in_time_ind] = 1.
        potential[delay_ind] = 1. - ( 0.01 * delay_week)
        potential[storage_ind] = 1. - ( 0.01 * storage_week)

    else:
        potential[storage_ind] = opportunity.revenue - \
                                    (opportunity.storage_cost * storage_week)

        potential[delay_ind] = opportunity.revenue - (opportunity.delay_cost * \
                                                                  delay_week)

        potential[in_time_ind] = opportunity.revenue

    return potential


def offer_potential(opportunity):
    '''
    '''
    if opportunity.production_line == 'Line1':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     init_potential(WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == 'Line2':
        potential = [init_potential(WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == '':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]

    return potential


def project_potential(opportunity):
    '''
    '''
    if opportunity.production_line == 'Line1':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     init_potential(WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == 'Line2':
        potential = [init_potential(WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == '':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]

    return potential


def batch_potential(opportunity):
    '''
    '''
    if opportunity.production_line == 'Line1':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     init_potential(WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == 'Line2':
        potential = [init_potential(WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == '':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]

    return potential


def internal_potential(opportunity):
    '''
    '''
    #:TODO Define an ad hoc potential for internal projects
    if opportunity.production_line == 'Line1':
        potential  = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                      init_potential(WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == 'Line2':
        potential = [init_potential(WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]
    elif opportunity.production_line == '':
        potential = [potential_shape(opportunity,WORKING_HOURS_PER_DAY_L1),
                     potential_shape(opportunity,WORKING_HOURS_PER_DAY_L2)]

    return potential

def get_potential(opportunity):
    '''
    '''
    if opportunity.kind == 'offer':
        potential = offer_potential(opportunity)
    elif opportunity.kind == 'project':
        potential = project_potential(opportunity)
    elif opportunity.kind == 'batch':
        potential = batch_potential(opportunity)
    elif opportunity.kind == 'internal':
        potential = internal_potential(opportunity)

    return potential


def init_potential(work_hours_per_day):
    '''
    '''
    schedule_days = (END_DATE - START_DATE).days
    schedule_hours = schedule_days * work_hours_per_day
    schedule = np.zeros(int(schedule_hours))
    return schedule


def init_schedule():
    '''
    '''
    schedule_days = (END_DATE - START_DATE).days
    schedule_hours_L1 = schedule_days * WORKING_HOURS_PER_DAY_L1
    schedule_hours_L2 = schedule_days * WORKING_HOURS_PER_DAY_L2
    schedule = np.array([np.ones(schedule_hours_L1) , \
                         np.ones(schedule_hours_L2)])
    return schedule


def optimizer(df):
    '''
    '''
    schedule = init_schedule()

    schedule2 = schedule - 1

    revenue = 0; margin = 0; probability = 1

    for index, opportunity in df.iterrows():

        potential = get_potential(opportunity)

        workload = int(np.ceil(opportunity.workload))

        overlap_L1 = np.floor(np.convolve(schedule[0],np.ones(workload), \
                                                    mode = 'valid') / workload)
        overlap_L2 = np.floor(np.convolve(schedule[1],np.ones(workload), \
                                                    mode = 'valid') / workload)

        pre_selec_L1 = np.append(np.zeros(workload-1), overlap_L1)
        pre_selec_L2 = np.append(np.zeros(workload-1), overlap_L2)

        pre_selec = np.array([pre_selec_L1 , pre_selec_L2])

        alloc = np.where(potential * pre_selec == np.max(potential * pre_selec))

        sel_line = alloc[0][0]
        sel_time = alloc[1][0]

        schedule[sel_line][sel_time - (workload - 1) : sel_time] = 0
        schedule2[sel_line][sel_time - (workload - 1) : sel_time] += 1

        df.set_value(index, 'sched_date', \
                            START_DATE + relativedelta(hours = int(sel_time)))

        index = df[df.cable_id == opportunity.cable_id].index.values
        if sel_line == 0:
            df.set_value(index, 'production_line' , 'Line1')
        else:
            df.set_value(index, 'production_line' , 'Line2')

        if opportunity.kind in ['project','offer','batch'] and \
            math.isnan(opportunity.revenue) == False:
            revenue += opportunity.revenue
            margin += opportunity.margin

        if opportunity.kind == 'offer' and \
            math.isnan(opportunity.probability) == False:
            probability *= opportunity.probability

    return {'revenue': revenue,
            'margin': margin,
            'probability': probability,
            }


def schedule():
    '''
    '''
    from BruggCablesKTI.db.model import DBHandler

    dbh = DBHandler('sqlite:///'+ DBFILE)

    #1.get the batches -> preliminary version
    schedules = dbh.schedules

    #2.generate a dictionary for the batch
    for schedule in schedules:

        import time; check_time = time.clock()


        batch_dictionary = []
        for slots in schedule.production_slots:

            # TODO: define the parameters in capital at the beginning of the script

            try:
                storage_cost = (slots.batch.cable.opportunity.revenue -
                                slots.batch.cable.opportunity.margin) * 0.045/52.
            except:
                storage_cost = None

            try:
                delay_cost = slots.batch.cable.opportunity.revenue / 100.
            except:
                delay_cost = None

            #TODO: solve the problem with the potential type
            if slots.batch.potential_type == 'project' and \
                 (slots.batch.cable.opportunity.revenue in [None,0]):
                 pot_type = 'internal'
            else:
                 pot_type = slots.batch.potential_type

            #TODO: selection line within the database
            prod_line = slots.batch.cable.production_line
            if pot_type in ['project', 'internal']:
                prod_line = slots.batch.cable.production_line
            elif pot_type in ['offer', 'batch'] and \
                    slots.batch.cable.production_line == '':
                if (slots.batch.cable.voltage > MAX_VOLTAGE_L1) or \
                    (slots.batch.cable.area > MAX_AREA_L1 ) or \
                    (slots.batch.cable.kind == 'SEG'):
                        prod_line = 'Line2'

            attr = {'cable_id': slots.batch.cable.pk,
                    'slots_id': slots.pk,
                    'revenue': slots.batch.cable.opportunity.revenue,
                    'workload': slots.batch.workload,
                    'delivery_date': slots.batch.delivery_date,
                    'storage_cost': storage_cost,
                    'delay_cost': delay_cost,
                    'kind': pot_type,
                    'margin': slots.batch.cable.opportunity.margin,
                    'probability': slots.batch.cable.opportunity.probability,
                    'production_line': prod_line,
                    'sched_date': None,}

            batch_dictionary.append(attr)
        df = pd.DataFrame(batch_dictionary)

        import pdb; pdb.set_trace()
        #3.Mandatory Operation: sort the DataFrame
        #TODO: solve the sorting problem on the revenue
        df.sort_values(['delivery_date', 'revenue'], \
                                ascending=[True, False], inplace = True)

        #4.Optimizer
        fitness = optimizer(df)

        #5. storage into the database
        sched_attr = [] # - > list of tuples containing the scheduling result

        probability = fitness['probability'] # -> fistness of the schedule
        margin = fitness['margin']
        revenue = fitness['revenue']

        for ind, row in df.iterrows():
            dbh.update_or_create_production_slot( pk = row.slots_id,
                                        end_time = row.sched_date,
                                        production_line = row.production_line )

            dbh.update_or_create_schedule(pk = schedule.pk, margin = margin,
                                                            revenue = revenue)

        print('Schedule computed in', time.clock() - check_time, 'seconds')



def main():
    tmp = schedule()

if __name__ == '__main__':
    main()
