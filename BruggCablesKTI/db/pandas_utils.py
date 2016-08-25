
import pandas as pd
import numpy as np

def get_opportunities_as_df(dbh):
    ''' Returns all opportunities in a pandas DataFrame.
    '''
    df = pd.DataFrame([
        {   "pk": opp.pk,
            "brugg_cables_id": opp.brugg_cables_id,
            "description": opp.description,
            "kind": opp.kind,
            "delivery_date": opp.delivery_date,
            "probability": opp.probability,
            "revenue": opp.revenue,
            "margin": opp.margin,
            "cables": ', '.join('{}: {}'.format(c.kind, c.voltage) for c in opp.cables),
            "workload": np.sum([b.workload for c in opp.cables for b in c.batches]),
            }
        for opp in dbh.opportunities ])
    return df


def get_cables_as_df(dbh):
    ''' Returns all batches in pandas DataFrame.
    '''
    df = pd.DataFrame([
        {   "pk": c.pk,
            "kind": c.kind,
            "voltage": c.voltage,
            "length": c.length,
            "area": c.area,
            "production_line": c.production_line,
            "opportunity_id": c.opportunity.brugg_cables_id,
            "opportunity_kind": c.opportunity.kind,
            "opportunity_probability": c.opportunity.probability,
            "opportunity_revenue": c.opportunity.revenue,
            "opportunity_margin": c.opportunity.margin,
            "opportunity_description": c.opportunity.description,
            }
        for c in dbh.cables ])
    return df


def get_batches_as_df(dbh):
    ''' Returns all batches in pandas DataFrame.
    '''
    df = pd.DataFrame([
        {   "pk": b.pk,
            "number": b.number,
            "potential": b.potential_type,
            "workload": b.workload,
            "delivery_date": b.delivery_date,
            "cable_pk": b.cable.pk,
            "cable_voltage": b.cable.voltage,
            "cable_area": b.cable.area,
            "cable_length": b.cable.length,
            "cable_kind": b.cable.kind,
            "cable_production_line": b.cable.production_line,
            "opportunity_id": b.cable.opportunity.brugg_cables_id,
            "opportunity_kind": b.cable.opportunity.kind,
            "opportunity_probability": b.cable.opportunity.probability,
            "opportunity_revenue": b.cable.opportunity.revenue,
            "opportunity_margin": b.cable.opportunity.margin,
            "opportunity_description": b.cable.opportunity.description,
            }
        for b in dbh.batches ])
    return df
