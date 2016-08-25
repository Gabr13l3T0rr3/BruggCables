
from BruggCablesKTI.db.model import FirstSelectionItem, FirstSelection, new_uuid

def write_first_selections_for_cluster(dbh, firstselections, **kwargs):
    ''' Writes a list of first selections (for a cluster) to the database.

    Arguments
    ---------
    dbh : a database handler
    firstselections : a list of lists of batch primary keys
    '''

    # BULK SAVE / INSERT FIRST_SELECTION OBJECTS
    first_selections = [ FirstSelection(pk=new_uuid(), **kwargs) for fs in firstselections ]

    for fs in first_selections:
        print(fs.pk)

    dbh.db.bulk_save_objects(first_selections)
    try:
        dbh.db.commit()
    except (IntegrityError, FlushError) as e:
        dbh.db.rollback()
        raise InvalidEntry(*e.args)

    #BULK INSERT FIRST_SELECTION_ITEM OBJECTS
    dbh.db.bulk_insert_mappings(FirstSelectionItem,
            [ { 'batch_pk': batch_pk,
                'first_selection_pk': first_selections[idx].pk }
                for idx, batches in enumerate(firstselections) for batch_pk in batches 
                ] )
    try:
        dbh.db.commit()
    except (IntegrityError, FlushError) as e:
        dbh.db.rollback()
        raise InvalidEntry(*e.args)    
