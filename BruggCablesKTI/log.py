import sys
import os
import logging

LOGGERNAME = 'BruggCables'

DEFAULT_STDOUTLOGLEVEL = 'DEBUG'
DEFAULT_FILELOGLEVEL = None 

DEFAULT_LOGFILE = 'BruggCables.log'


global_logger = None

def get_logger(**kwargs):
    '''
    Get the global logger
    '''
    global global_logger
    if global_logger is None:
        global_logger = _create_logger(**kwargs)
    return global_logger

def _create_logger(**kwargs):

    logfile = kwargs.get('logfile', DEFAULT_LOGFILE)
    fileloglevel = kwargs.get('fileloglevel', DEFAULT_FILELOGLEVEL)
    stdoutloglevel = kwargs.get('stdoutloglevel', DEFAULT_STDOUTLOGLEVEL)

    logger = logging.getLogger(LOGGERNAME)
    logger.setLevel(logging.DEBUG)

    # FORMATTER
    formatter = logging.Formatter(
            '[ %(asctime)s ] [ %(module)s ] [ %(levelname)s ] : %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S')

    # LOG to file
    if not (fileloglevel is None):
        # make sure the logfile directory exists, create it otherwise
        path, fn = os.path.split(os.path.normpath(os.path.abspath(logfile)))
        if not os.path.exists(path): os.makedirs(path)
        fh = logging.FileHandler(os.path.join(path, fn), mode='w')
        fh.setLevel(getattr(logging, fileloglevel))
        fh.setFormatter(formatter)
        logger.addHandler(fh)

    # LOG to stdout
    if not (stdoutloglevel is None):
        ch = logging.StreamHandler(sys.stdout)
        ch.setLevel(getattr(logging, stdoutloglevel))
        ch.setFormatter(formatter)
        logger.addHandler(ch)

    logger.debug('stdoutloglevel: '+str(stdoutloglevel))
    logger.debug('fileloglevel: '+str(fileloglevel))
    logger.debug('logfile: '+str(logfile))

    return logger
