
import json

import numpy as np

import pandas as pd
from datetime import datetime, timedelta

from BruggCablesKTI.db import model

from BruggCablesKTI.simulation.production_batches import calculate_batches

from BruggCablesKTI import log

logger = log.get_logger()


BATCH_DELAY = 21  # days

WORKHOURS_PER_WEEK = 7*24

def import_opportunities_from_xlsx(xlsx_file, dbfile):


    db_url = 'sqlite:///'+dbfile
    logger.debug('Creating Handler for db at '+db_url)
    dbh = model.DBHandler(db_url)

    logger.debug('reading excel file : ' + xlsx_file)
    df = pd.read_excel(xlsx_file)

    for record in df.to_dict(orient='records'):

        try:
            opp_id = int(record['Nr.'])
        except:
            opp_id = None

        try:
            revenue = float(record['Betrag Standartwährung'].replace('CHF', '').replace(',', ''))
        except:
            revenue = None

        try:
            margin = float(record['Profit Margin in % (DB1)'])/100.*revenue
        except:
            margin = None

        try:
            probability = float(record['Wahrscheinlichkeit (%)'])
        except:
            probability = None

        try:
            delivery_date = datetime.strptime(str(record['Liefertermin Ist']),'%Y-%m-%d %H:%M:%S')
        except:
            delivery_date = None

        try:
            area = float(record['Querschnitt mm?'])
        except:
            area = None

        try:
            voltage = float(record['Spannungsebene KV'])
        except:
            voltage = None

        try:
            length = float(record['Menge (m)'])
        except:
            length = None
    
        try:
            cable_type = "RMV" if area < 1000 else "SEG"
        except:
            cable_type = None

        try:
            area2 = float(record['Querschnitt mm? (2)'])
        except:
            area2 = None

        try:
            voltage2 = float(record['Spannungsebene KV (2)'])
        except:
            voltage2 = None

        try:
            length2 = float(record['Menge (m) (2)'])
        except:
            length2 = None

        if np.isnan(area2) or np.isnan(voltage2):
            cable_type2 = None
        else:
            try:
                cable_type2 = "RMV" if area2 < 1000 else "SEG"
            except:
                cable_type2 = None

        production_line = ''
        kind = 'offer'

        opp_attrs = {
                'description': record['Verkaufschance Bezeichnung'],
                'margin': margin,
                'revenue': revenue,
                'probability': probability,
                'delivery_date': delivery_date,
                'kind': kind ,
                }

        joppattrs = opp_attrs.copy()
        joppattrs['delivery_date'] = str(joppattrs['delivery_date'])
        logger.debug('update or add opportunity: ' + json.dumps(joppattrs))
        opp = dbh.update_or_create_opportunity(opp_id, **opp_attrs)

        cable1_attrs = {
                'kind': cable_type,
                'voltage': voltage,
                'area': area,
                'length': length,
                'production_line': production_line,
                'opportunity_pk': opp.pk,
                }

        logger.debug('update or add cable: ' + json.dumps(cable1_attrs))
        dbh.add_cable(**cable1_attrs)

        cable2_attrs = {
                'kind': cable_type2,
                'voltage': voltage2,
                'area': area2,
                'length': length2,
                'production_line': production_line,
                'opportunity_pk': opp.pk,
                }

        if cable2_attrs['kind'] not in ['', None]:
            logger.debug('update or add cable: ' + json.dumps(cable2_attrs))
            dbh.add_cable(**cable2_attrs)


def import_projects_from_xlsx(xlsx_file, dbfile):

    db_url = 'sqlite:///'+dbfile
    dbh = model.DBHandler(db_url)

    df = pd.read_excel(xlsx_file)

    for record in df.to_dict(orient='records'):

        try:
            opp_id = int(record['Auftrag'])
        except:
            opp_id = None

        try:
            revenue = float(record['Revenue (CHF)'])
        except:
            revenue = None

        try:
            margin = float(record['Margin (%)'])/100.*revenue
        except:
            margin = None

        try:
            delivery_date = datetime.strptime(str(record['LT']),'%Y-%m-%d %H:%M:%S')
        except:
            delivery_date = None

        try:
            workload = float(record['Zeit'])
        except:
            workload = None

        try:
            area = float(record['Querschnitt mm?'])
        except:
            area = None

        try:
            length = float(record['Menge (m)'])
        except:
            length = None

        try:
            voltage = float(record['Spannungsebene KV'])
        except:
            voltage = None

        try:
            cable_type = "RMV" if area < 1000 else "SGM"
        except:
            cable_type = None

        try:
            area2 = float(record['Querschnitt mm? (2)'])
        except:
            area2 = None

        try:
            voltage2 = float(record['Spannungsebene KV (2)'])
        except:
            voltage2 = None

        try:
            length2 = float(record['Menge (m) (2)'])
        except:
            length2 = None

        if area2 in ['', None] or voltage2 in ['', None]:
            cable_type2 = None
        else:
            try:
                cable_type2 = "RMV" if area2 < 1000 else "SEG"
            except:
                cable_type2 = None

        try:
            production_line = record['Linie'].replace('Linie', 'Line')
        except:
            production_line = ''

        try:
            remarks = record['Remarks']
        except:
            remarks = ''

        if remarks in ['Medium Voltage', 'Medium Voltage for stock', 'internal project', ]:
            kind = 'internal'
        else:
            kind = 'project'

        opp_attrs = {
                'description': record['Bezeichnung'],
                'margin': margin,
                'revenue': revenue,
                'probability': 1.,
                'delivery_date': delivery_date,
                'kind': kind ,
                }

        opp = dbh.update_or_create_opportunity(opp_id, **opp_attrs)

        cable1_attrs = {
                'kind': cable_type,
                'voltage': voltage,
                'area': area,
                'length': length,
                'production_line': production_line,
                'opportunity': opp,
                }

        dbh.update_or_create_cable(None, **cable1_attrs)

        cable2_attrs = {
                'kind': cable_type2,
                'voltage': voltage2,
                'area': area2,
                'length': length2,
                'production_line': production_line,
                'opportunity': opp,
                }

        if cable2_attrs['kind'] not in ['', None]:
            dbh.update_or_create_cable(None, **cable2_attrs)


def generate_batches(dbfile, projects_xlsx_file):
    ''' Generates and persists the batches to be produced to the db.
    '''

    df = pd.read_excel(projects_xlsx_file)

    db_url = 'sqlite:///'+dbfile
    dbh = model.DBHandler(db_url)

    for cable in dbh.cables:

        if cable.opportunity.kind == 'offer':

            ## XXX :: STUPID HACK !! TODO NEED TO MAKE THIS TRANSPARENT
            if cable.area == None:
                continue
            if cable.opportunity.delivery_date == None:
                continue
            
            batches = calculate_batches(cable.length, cable.voltage, cable.area)

        else:

            #print('Brugg Cables ID: ', cable.opportunity.brugg_cables_id)
            #print('Zeit: ', df[df.Auftrag == cable.opportunity.brugg_cables_id]['Zeit'])

            workload = float(df[df.Auftrag == cable.opportunity.brugg_cables_id ]['Zeit'])
            if workload < WORKHOURS_PER_WEEK:
                batches = [workload, ]
            else:
                batches = [ WORKHOURS_PER_WEEK, ]*int(workload/WORKHOURS_PER_WEEK)
                batches.append(np.mod(workload, WORKHOURS_PER_WEEK))

        #print(batches)

        for idx, batch in enumerate(batches):

            # XXX STUPID HACK :: TODO :: FIX THIS
            if cable.opportunity.delivery_date is None:
                continue
            delivery_date = cable.opportunity.delivery_date+timedelta(idx*BATCH_DELAY)

            if cable.opportunity.kind == 'offer':
                if idx == 0:
                    potential_type = 'offer'
                else:
                    potential_type = 'batch'
            elif cable.opportunity.kind == 'project':
                potential_type = 'project'
            elif cable.opportunity.kind == 'internal':
                potential_type = 'internal'

            dbh.update_or_create_batch(
                    cable_pk=cable.pk,
                    number=idx,
                    delivery_date=delivery_date,
                    workload=batch,
                    potential_type=potential_type,
                    )
