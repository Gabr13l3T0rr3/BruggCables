
import math
import numpy as np


# TODO
# list all constants at beginning of file named in CAPITAL_LETTERS
# document what the source of the constants is
# put the reference files into specific folder


DENSITY_ISOLATION = 922  # units?
BATCH = 45000 # what's this?

DIAMETER = np.array([
    17.3, 19.3, 21.2, 23.1, 25.0, 26.7, 28.4, 30.1, 31.7, 33.3, 34.8, 36.2,
    37.6, 39.0, 40.3, 41.5, 42.8, 43.9, 47.1, 48.2, 49.2, 50.3, 51.3, 52.2,
    53.2, 54.1, 54.9, 55.8, 56.6, 57.4, 58.2, 59.0, 59.7, 60.4, 61.1, 61.8,
    62.5, 63.2, 63.8, 64.5, 65.1, 65.7, 66.4, 67.0, 67.6, 68.2, 68.8, 69.5 ])

AREA = np.array([
    150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800, 850,
    900, 950, 1000, 1050, 1100, 1150, 1200, 1250, 1300, 1350, 1400, 1450, 1500,
    1550, 1600, 1650, 1700, 1750, 1800, 1850, 1900, 1950, 2000, 2050, 2100,
    2150, 2200, 2250, 2300, 2350, 2400, 2450, 2500 ])


VOLTAGE = np.array([ 132., 220., 275., 330., 380., 420., 500. ])
WANDST = np.array([ 15, 18, 20, 22, 24, 26, 30 ])


def get_diameter_from_area(area):
    '''
    '''
    return DIAMETER[np.argmin(np.abs(area - AREA))]


def get_isolation_from_voltage(voltage):
    '''
    '''
    return WANDST[np.argmin(np.abs(voltage - VOLTAGE))]


def calculate_batches(length, voltage, area):
    '''
    '''

    Diam = get_diameter_from_area(area)
    IsolationTick = get_isolation_from_voltage(voltage)
    IsolationDiameter = Diam + 2. * IsolationTick
    IsolationSize = ((IsolationDiameter + 2)**2*math.pi/4.) - ((Diam - 3)**2 * math.pi / 4.)

    # TODO : what are the unknown constants in the following lines?
    LineSpeed = 4266.8 * (IsolationSize ** -0.9909) * 60.
    ProductionLimit = BATCH / (IsolationSize / 1e6 * DENSITY_ISOLATION)

    if length > ProductionLimit:
        batches = [ProductionLimit] * int(length / ProductionLimit)
        batches.append(length - np.sum(batches))
        batches = np.asarray(batches) / LineSpeed
    else:
        batches = [length/LineSpeed]

    return batches
