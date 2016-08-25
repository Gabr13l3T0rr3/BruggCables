#!/usr/bin/env python

'''
This script creates as diagram figure of the data model for the specified model
classes.

It requires the python package sadisplay (install with $ pip install sadisplay)

and graphviz (install with homebrew : $ brew install graphviz)


'''

import sys, subprocess, os

try:
    import sadisplay
except:
    sys.exit('Could not import sadisplay! Aborted.')

from BruggCablesKTI.db import model

MODEL_CLASSES = ['Opportunity', 'Cable', 'Batch', 'FirstSelection',
        'FirstSelectionItem', 'ProductionSlot', 'Schedule']

desc = sadisplay.describe(
    [getattr(model, attr) for attr in MODEL_CLASSES],
    show_methods=True,
    show_properties=True,
    show_indexes=True,
)

open(os.path.expanduser('~/Desktop/schema.dot'), 'w').write(sadisplay.dot(desc))

try:
    p = subprocess.Popen(['dot', '-Tpdf', 
        os.path.expanduser('~/Desktop/schema.dot'), 
        '-o', os.path.expanduser('~/Desktop/schema.pdf'),])
    stdout, stderr = p.communicate()
    print('Wrote schema diagram as schema.pdf to Desktop.')
except:
    print('Cannot convert schmema.dot to .png using graphviz.')
    print('Is graphviz installed?')
    sys.exit(1)
