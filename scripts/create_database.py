#!/usr/bin/env python
import os

print('\n\n.. STARTING TO IMPORT DATA INTO NEW DATABASE ..\n')

from BruggCablesKTI import log
logger = log.get_logger()

from BruggCablesKTI.db import import_data

dbfile_path = os.path.normpath(os.path.join(os.getcwd(), '../../../BruggCables_michele.db'))

projects_file = os.path.normpath(os.path.join(os.getcwd(), '../../../../Data/Lines.xlsx'))
offers_file = os.path.normpath(os.path.join(os.getcwd(),
    '../../../../Data/Open_Opportunities.xlsx'))

if os.path.exists(dbfile_path):
    logger.info('old database file removed.')
    os.remove(dbfile_path)


import_data.import_opportunities_from_xlsx(offers_file, dbfile_path)
logger.info('successfully created databasefile at {}\n .. offer data imported.'.format(dbfile_path))

import_data.import_projects_from_xlsx(projects_file, dbfile_path)
logger.info(' .. project data imported.'.format(dbfile_path))

import_data.generate_batches(dbfile_path, projects_file)
logger.info(' .. batches generated.'.format(dbfile_path))

