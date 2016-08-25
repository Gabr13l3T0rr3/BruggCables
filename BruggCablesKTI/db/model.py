'''
Module defining the datamodel for the BruggCables Opportunity Simulator.

'''

import os
import sys
import datetime


from sqlalchemy import Column, ForeignKey, Integer, String, Float, DateTime,\
        Enum
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm.exc import NoResultFound, FlushError
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import sessionmaker, scoped_session, relationship,\
        column_property
from sqlalchemy import create_engine, and_, or_, select

from uuid import uuid4

from BruggCablesKTI.simulation.production_batches import calculate_batches


'''
TODO michael ::
- batch saving first_selections (batches)
- profile saving
- saving lists of first selections (clusterwise)
- investigate size of primary keys -> how to get db size down?


'''


class InvalidEntry(ValueError):
    pass
class MissingEntry(ValueError):
    pass


def new_uuid():
    return uuid4().hex

Base = declarative_base()


# DATA MODEL
###############################################################################

class Opportunity(Base):
    ''' 'Offers' AND 'Projects' !

    .. consisting potentially of multiple cables.
    '''
    __tablename__ = 'opportunities'

    # primary key
    pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    cables = relationship("Cable", back_populates="opportunity")

    # the Brugg Cables id
    brugg_cables_id = Column(Integer, unique=True)

    # a short description of the opportunity
    description = Column(String(256), nullable=False)

    # the margin associated with the opportunity in sFr.
    margin = Column(Float)

    # the revenue associated with the opportunity in sFr.
    revenue = Column(Float)

    # the revenue associated with the opportunity in sFr.
    probability = Column(Float)

    # the delivery date associated with the opportunity
    delivery_date = Column(DateTime)

    # 'offer' or 'project'
    KINDS = ('offer', 'project', 'internal')
    kind = Column(Enum(*KINDS))

    def __repr__(self):
        return "Opportunity<{}, {}>".format(self.brugg_cables_id,
                self.description)

    def __str__(self):
        return "Opportunity<{}, {}>".format(self.brugg_cables_id,
                self.description)

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            "description": self.description,
            "brugg_cables_id": self.brugg_cables_id,
            "margin": self.margin,
            "revenue": self.revenue,
            "probability": self.probability,
            "delivery_date": self.delivery_date,
            "kind":self.kind,
        }


class Cable(Base):
    ''' Database table individual products.
    '''
    __tablename__ = 'cables'

    # primary key
    pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    opportunity_pk = Column(String(32), ForeignKey('opportunities.pk'),
            nullable=False)
    opportunity = relationship("Opportunity", back_populates="cables")
    batches = relationship("Batch", back_populates="cable")

    # the cable type/kind associated with the opportunity
    KINDS = ('SEG', 'RMV')
    kind = Column(Enum(*KINDS))

    # the voltage level associated with the opportunity
    voltage = Column(Float)

    # the cable area associated with the opportunity
    area = Column(Float)

    # the cable length associated with the opportunity
    length = Column(Float)

    # the production line associated with the opportunity
    PRODUCTION_LINES = ('Line1', 'Line2', '')
    production_line = Column(Enum(*PRODUCTION_LINES))

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            "kind": self.kind,
            "voltage": self.voltage,
            "area": self.area,
            "length": self.length,
            "production_line": self.production_line,
            "opportunity": self.opportunity.brugg_cables_id,
        }

    def __repr__(self):
        return "Cable<{}, {}, {}>".format(self.opportunity.brugg_cables_id,
                self.kind, self.voltage)

    def __str__(self):
        return "Cable<{}, {}, {}>".format(self.opportunity.brugg_cables_id,
                self.kind, self.voltage)


class FirstSelectionItem(Base):
    '''
    '''
    __tablename__ = 'first_selection_items'

    # primary key
    #pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    batch_pk = Column(String(32), ForeignKey('batches.pk'), primary_key=True)
    batch = relationship("Batch", back_populates="first_selections")
    first_selection_pk = Column(String(32), ForeignKey('first_selections.pk'), primary_key=True)
    first_selection = relationship("FirstSelection", back_populates="batches")

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            "batch": self.batch.pk,
            "first_selection": self.first_selection.pk,
        }

    def __repr__(self):
        return "FirstSelectionItem<batch: {}, first_selection: {}>".format(
                self.batch.pk, self.first_selection.pk)

    def __str__(self):
        return "FirstSelectionItem<batch: {}, first_selection: {}>".format(
                self.batch.pk, self.first_selection.pk)


class Batch(Base):
    ''' An Cable is chunked into batches for production, if necessary ..
    '''
    __tablename__ = 'batches'

    # primary key
    pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    cable_pk = Column(String(32), ForeignKey('cables.pk'))
    cable = relationship("Cable", back_populates="batches")

    first_selections = relationship("FirstSelectionItem", back_populates="batch",)
            #secondary="first_selection_items")

    production_slots = relationship("ProductionSlot", back_populates="batch")

    # the number in the row of batches for this production
    number = Column(Integer, nullable=False)

    # the date this batch has to be delivered
    delivery_date = Column(DateTime)

    # the potential function to be used to schedule this batch
    # 'offer', 'project', 'batch', 'internal',
    POTENTIAL_TYPES = ('offer','project','batch','internal')
    potential_type = Column(Enum(*POTENTIAL_TYPES))

    # the production workload in hours
    workload = Column(Float)

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            "number": self.number,
            "potential": self.potential_type,
            "workload": self.workload,
            "cable": self.cable.pk,
            "delivery_date": self.delivery_date,
            "opportunity": self.cable.opportunity.brugg_cables_id,
        }

    def __repr__(self):
        return "Batch<{}, {}, {}, {}>".format(
                self.cable.opportunity.brugg_cables_id, self.cable.kind,
                self.cable.voltage, self.number)

    def __str__(self):
        return "Batch<{}, {}, {}, {}>".format(
                self.cable.opportunity.brugg_cables_id, self.cable.kind,
                self.cable.voltage, self.number)


class Schedule(Base):
    '''
    '''
    __tablename__ = 'schedules'

    # primary key
    pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    production_slots = relationship("ProductionSlot", back_populates="schedule")

    # the margin calculated for the entire schedule in sFr.
    margin = Column(Float)

    # the revenue calculated for the entire schedule in sFr.
    revenue = Column(Float)

    @property
    def batches(self):
        return [ps.batch for ps in self.production_slots ]

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            #"production_slots": self.production_slots,
        }

    def __repr__(self):
        return "Schedule<{}>".format(self.pk)

    def __str__(self):
        return "Schedule<{}>".format(self.pk)


class ProductionSlot(Base):
    '''
    '''
    __tablename__ = 'production_slots'

    # primary key
    pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    batch_pk = Column(String(32), ForeignKey('batches.pk'), nullable=False)
    batch = relationship("Batch", back_populates="production_slots")
    schedule_pk = Column(String(32),
            ForeignKey('schedules.pk'), nullable=False)
    schedule = relationship("Schedule", back_populates="production_slots")

    # attributes
    end_time = Column(DateTime)

    PRODUCTION_LINES = ('Line1', 'Line2')
    production_line = Column(Enum(*PRODUCTION_LINES))

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            "batch": self.batch.pk,
            "schedule": self.schedule.pk,
        }

    def __repr__(self):
        return "FirstSelectionItem<{}>".format(self.pk)

    def __str__(self):
        return "FirstSelectionItem<{}>".format(self.pk)


class FirstSelection(Base):
    '''
    '''
    __tablename__ = 'first_selections'

    # primary key
    pk = Column(String(32), primary_key=True, default=new_uuid)

    # relationships
    batches = relationship("FirstSelectionItem", back_populates="first_selection",)
            #secondary="first_selection_items")

    # cluster number
    cluster_number = Column(Integer)

    #@property
    #def batches(self):
        #return [it.batch for it in self.first_selection_items]

    def to_dict(self):
        """ Convert the object to a JSON-friendly dictionary representation.
        """
        return {
            "pk": self.pk,
            #"first_selection": self.first_selection_items,
        }

    def __repr__(self):
        return "FirstSelection<{}>".format(self.pk)

    def __str__(self):
        return "FirstSelection<{}>".format(self.pk)


# DATABASE HANDLER
###############################################################################

class DBHandler(object):
    """ The DBHandler object to interface with the database holding
    opportunities, schedules, ..

    """

    def __init__(self, db_url='sqlite:///BruggCables.db'):
        """ Initialize the connection to the database.

        Arguments
        ---------
        db_url : string
            The URL to the database, e.g. ``sqlite:///BruggCables.db``
        """
        # create the connection to the database
        engine = create_engine(db_url)
        self.db = scoped_session(sessionmaker(autoflush=True, bind=engine))

        # this creates all the tables in the database if they don't already exist
        Base.metadata.create_all(bind=engine)


    # OPPORTUNITIES -----------------------------------------------------------
    @property
    def opportunities(self):
        """ A list of all opportunities in the database."""
        return self.db.query(Opportunity).all()

    def add_opportunity(self, brugg_cables_id, **kwargs):
        """ Add a new opportunity to the database.

        Arguments
        ---------
        description : string
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.Opportunity`

        Returns
        -------
        opportunity : :class:`BruggCablesKTI.db.model.Opportunity`
        """
        opportunity = Opportunity(brugg_cables_id=brugg_cables_id, **kwargs)
        self.db.add(opportunity)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
        return opportunity


    def find_opportunity(self, brugg_cables_id):
        """ Find a opportunity.

        Arguments
        ---------
        brugg_cables_id: string
            The id of the opportunity used by Brugg Cables.

        Returns
        -------
        opportunity : :class:`BruggCablesKTI.db.model.Opportunity`
        """
        try:
            opportunity = self.db.query(Opportunity)\
                .filter(Opportunity.brugg_cables_id == brugg_cables_id)\
                .one()
        except NoResultFound:
            raise MissingEntry("No such Opportunity: {}".format(brugg_cables_id))
        return opportunity


    def update_or_create_opportunity(self, brugg_cables_id, **kwargs):
        """ Update an existing opportunity, or create it if it doesn't exist.

        Arguments
        ---------
        brugg_cables_id: int
            the Brugg Cables internal id of the opportunity
        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.Opportunity`

        Returns
        -------
        opportunity : :class:`BruggCablesKTI.db.model.Opportunity`
        """
        try:
            opportunity = self.find_opportunity(brugg_cables_id)
        except MissingEntry:
            opportunity = self.add_opportunity(brugg_cables_id, **kwargs)
        else:
            for attr in kwargs:
                setattr(opportunity, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return opportunity


    def remove_opportunity(self, brugg_cables_id):
        """ Deletes an existing Opportunity from the database.

        Arguments
        ---------
        brugg_cables_id: int
            the Brugg Cables internal id of the opportunity to delete
        """
        opportunity = self.find_opportunity(brugg_cables_id)
        # delete all associated batches too
        for cable in opportunity.cables:
            self.remove_cable(cable.pk)
        self.db.delete(opportunity)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)


    def select_opportunity(self, start_date, end_date):
        """ return opportunities with delivery date within a
            specified time range.

        Arguments
        ---------
        start_date : start date of the selected interval
        end_date : end date of the selected interval

        Returns
        -------
        opportunity : :class:`BruggCablesKTI.db.model.Opportunity`
        """

        time_statement = and_(Opportunity.delivery_date < end_date, \
                              Opportunity.delivery_date >= start_date)

        return self.db.query(Opportunity).filter(time_statement).all()




    # CABLES ------------------------------------------------------------------
    @property
    def cables(self):
        """ A list of all cables in the database."""
        return self.db.query(Cable).all()

    def add_cable(self, opportunity_pk, **kwargs):
        """ Add a new cable to the database.

        Arguments
        ---------
        opportunity_pk : string
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.Cable`

        Returns
        -------
        cable: :class:`BruggCablesKTI.db.model.Cable`
        """
        cable = Cable(opportunity_pk=opportunity_pk, **kwargs)
        self.db.add(cable)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
        return cable

    def find_cable(self, pk):
        """ Find a cable.

        Arguments
        ---------
        pk: string
            The primary key used in our database table.

        Returns
        -------
        cable: :class:`BruggCablesKTI.db.model.Cable`
        """
        try:
            cable = self.db.query(Cable).filter(Cable.pk == pk).one()
        except NoResultFound:
            raise MissingEntry("No such Cable: {}".format(pk))
        return cable

    def update_or_create_cable(self, pk, **kwargs):
        """ Update an existing cable, or create it if it doesn't exist.

        Arguments
        ---------
        pk: string
            The primary key used in our database table.

        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.Cable`

        Returns
        -------
        cable: :class:`BruggCablesKTI.db.model.Cable`
        """
        try:
            cable = self.find_cable(pk)
        except MissingEntry:
            opportunity_pk = kwargs.pop('opportunity_pk', None)
            cable = self.add_cable(opportunity_pk, **kwargs)
        else:
            for attr in kwargs:
                setattr(cable, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return cable

    def remove_cable(self, pk):
        """ Deletes an existing Cable from the database.

        Arguments
        ---------
        pk: string
            The primary key used in our database table.
        """
        cable = self.find_cable(pk)
        # delete all associated batches too
        for batch in cable.cables:
            self.remove_batch(batch.pk)
        self.db.delete(cable)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)


    # BATCHES -----------------------------------------------------------------
    @property
    def batches(self):
        """ A list of all batches in the database."""
        return self.db.query(Batch).all()

    def add_batch(self, **kwargs):
        """ Add a new batch to the database.

        Arguments
        ---------
        description : string
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.Batch`

        Returns
        -------
        batch: :class:`BruggCablesKTI.db.model.Batch`
        """
        batch = Batch(**kwargs)
        self.db.add(batch)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
        return batch

    def find_batch(self, pk):
        """ Find a batch.

        Arguments
        ---------
        pk : string
            The primary key of the Batch used in our table.

        Returns
        -------
        batch: :class:`BruggCablesKTI.db.model.Batch`
        """
        try:
            batch = self.db.query(Batch)\
                .filter(Batch.pk == pk)\
                .one()
        except NoResultFound:
            raise MissingEntry("No such Batch: {}".format(pk))
        return batch

    def update_or_create_batch(self, pk=None, **kwargs):
        """ Update an existing batch, or create it if it doesn't
        exist.

        Arguments
        ---------
        pk: str
            the primary key of Batch used in our table
        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.Batch`

        Returns
        -------
        batch: :class:`BruggCablesKTI.db.model.Batch`
        """
        try:
            batch = self.find_batch(pk)
        except MissingEntry:
            batch = self.add_batch(**kwargs)
        else:
            for attr in kwargs:
                setattr(batch, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return batch

    def remove_batch(self, pk):
        """ Deletes an existing Batch from the database.

        Arguments
        ---------
        pk: str
            the primary key of Batch used in our table
        """
        batch = self.find_batch(pk)
        # delete all associated first_selection_items too
        for fsi in batch.first_selection_items:
            self.remove_first_selection_item(fsi.pk)
        self.db.delete(batch)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)

    def select_batch_timerange(self, start_date, end_date, batch_number=0):
            """ return batches with delivery date within a
                specified time range.

            Arguments
            ---------
            start_date : start date of the selected interval
            end_date : end date of the selected interval

            batch_number : (defaults to 0) the batch number

            Returns
            -------
            list of batches : :class:`BruggCablesKTI.db.model.Batch`
            """

            selection = and_(Batch.delivery_date < end_date, \
                             Batch.delivery_date >= start_date,
                             Batch.number==batch_number)

            return self.db.query(Batch).filter(selection).all()


    # FIRST_SELCTION ----------------------------------------------------------
    @property
    def first_selections(self):
        """ A list of all first_selections in the database."""
        return self.db.query(FirstSelection).all()

    def add_first_selection(self, batches=[], **kwargs):
        """ Add a new first_selection to the database.

        Arguments
        ---------
        description : string
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.FirstSelection`

        Returns
        -------
        first_selection: :class:`BruggCablesKTI.db.model.FirstSelection`
        """

        first_selection = FirstSelection(**kwargs)
        self.db.add(first_selection)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)

        # DEPRECATED : massive speedups by bulk_insert
        #for batch_pk in batches:
            #batch_object = self.find_batch(batch_pk)
            #self.add_first_selection_item(batch=batch_object,
                    #first_selection=first_selection)
    
        # BULK INSERT FIRST SELECTION ITEMS
        self.db.bulk_insert_mappings(FirstSelectionItem,
                [ { 'batch_pk': batch_pk,
                    'first_selection_pk': first_selection.pk }
             for batch_pk in batches ])
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)    

        return first_selection

    def find_first_selection(self, pk):
        """ Find a first_selection.

        Arguments
        ---------
        pk : string
            The primary key of the FirstSelection used in our table.

        Returns
        -------
        first_selection: :class:`BruggCablesKTI.db.model.FirstSelection`
        """
        try:
            first_selection = self.db.query(FirstSelection)\
                .filter(FirstSelection.pk == pk)\
                .one()
        except NoResultFound:
            raise MissingEntry("No such FirstSelection: {}".format(pk))
        return first_selection

    def update_or_create_first_selection(self, pk=None, **kwargs):
        """ Update an existing first_selection, or create it if it doesn't
        exist.

        Arguments
        ---------
        pk: str
            the primary key of FirstSelection used in our table
        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.FirstSelection`

        Returns
        -------
        first_selection: :class:`BruggCablesKTI.db.model.FirstSelection`
        """
        try:
            first_selection = self.find_first_selection(pk)
        except MissingEntry:
            first_selection = self.add_first_selection(**kwargs)
        else:
            for attr in kwargs:
                setattr(first_selection, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return first_selection

    def remove_first_selection(self, pk):
        """ Deletes an existing FirstSelection from the database.

        Arguments
        ---------
        pk: str
            the primary key of FirstSelection used in our table
        """
        first_selection = self.find_first_selection(pk)
        # delete all associated first_selection_items too
        for fsi in first_selection.first_selection_items:
            self.remove_first_selection_item(fsi.pk)
        self.db.delete(first_selection)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)

    def select_first_selection(self, cluster_number, element_number = None):
            """ returns first selection items for a given cluster.

            Arguments
            ---------
            cluster_number : number of the selected clister

            Returns
            -------
            list of first selections : :class:`BruggCablesKTI.db.model.FirstSelection`
            """

            selection = FirstSelection.cluster_number == cluster_number
            if element_number == None:
                return self.db.query(FirstSelection.pk).filter(selection).all()
            else:
                return (self.db.query(FirstSelection.pk).
                            filter(selection))[element_number][0]

    # FIRST_SELCTION_ITEMS ----------------------------------------------------
    @property
    def first_selection_items(self):
        """ A list of all first_selection_items in the database."""
        return self.db.query(FirstSelectionItem).all()

    def add_first_selection_item(self, **kwargs):
        """ Add a new first_selection_item to the database.

        Arguments
        ---------
        description : string
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.FirstSelectionItem`

        Returns
        -------
        first_selection_item:
            :class:`BruggCablesKTI.db.model.FirstSelectionItem`
        """
        first_selection_item = FirstSelectionItem(**kwargs)
        self.db.add(first_selection_item)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
        return first_selection_item

    def find_first_selection_item(self, pk):
        """ Find a first_selection_item.

        Arguments
        ---------
        pk : string
            The primary key of the FirstSelectionItem used in our table.

        Returns
        -------
        first_selection_item: :class:`BruggCablesKTI.db.model.FirstSelectionItem`
        """
        try:
            first_selection_item = self.db.query(FirstSelectionItem)\
                .filter(FirstSelectionItem.pk == pk)\
                .one()
        except NoResultFound:
            raise MissingEntry("No such FirstSelectionItem: {}".format(pk))
        return first_selection_item

    def update_or_create_first_selection_item(self, pk=None, **kwargs):
        """ Update an existing first_selection_item, or create it if it doesn't
        exist.

        Arguments
        ---------
        pk: str
            the primary key of FirstSelectionItem used in our table
        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.FirstSelectionItem`

        Returns
        -------
        first_selection_item: :class:`BruggCablesKTI.db.model.FirstSelectionItem`
        """
        try:
            first_selection_item = self.find_first_selection_item(pk)
        except MissingEntry:
            first_selection_item = self.add_first_selection_item(**kwargs)
        else:
            for attr in kwargs:
                setattr(first_selection_item, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return first_selection_item

    def remove_first_selection_item(self, pk):
        """ Deletes an existing FirstSelectionItem from the database.

        Arguments
        ---------
        pk: str
            the primary key of FirstSelectionItem used in our table
        """
        first_selection_item = self.find_first_selection_item(pk)
        self.db.delete(first_selection_item)

        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)


    # SCHEDULE ----------------------------------------------------------------
    @property
    def schedules(self):
        """ A list of all schedules in the database."""
        return self.db.query(Schedule).all()

    def add_schedule(self, batches_end_times=[], **kwargs):
        """ Add a new schedule to the database.

        Arguments
        ---------
        batches_end_times : list of tuples [(batch_pk, end_time), .. ]
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.Schedule`

        Returns
        -------
        schedule: :class:`BruggCablesKTI.db.model.Schedule`
        """

        schedule = Schedule(**kwargs)
        self.db.add(schedule)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
    
        # BULK INSERT ProductionSlot's 
        self.db.bulk_insert_mappings(ProductionSlot,
                [ { 'batch_pk': batch_pk,
                    'end_time': end_time,
                    'schedule_pk': schedule.pk }
             for (batch_pk, end_time) in batches_end_times ])
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)    

        return schedule

    def find_schedule(self, pk):
        """ Find a schedule.

        Arguments
        ---------
        pk : string
            The primary key of the Schedule used in our table.

        Returns
        -------
        schedule: :class:`BruggCablesKTI.db.model.Schedule`
        """
        try:
            schedule = self.db.query(Schedule)\
                .filter(Schedule.pk == pk)\
                .one()
        except NoResultFound:
            raise MissingEntry("No such Schedule: {}".format(pk))
        return schedule

    def update_or_create_schedule(self, pk=None, **kwargs):
        """ Update an existing schedule, or create it if it doesn't
        exist.

        Arguments
        ---------
        pk: str
            the primary key of Schedule used in our table
        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.Schedule`

        Returns
        -------
        schedule: :class:`BruggCablesKTI.db.model.Schedule`
        """
        try:
            schedule = self.find_schedule(pk)
        except MissingEntry:
            schedule = self.add_schedule(**kwargs)
        else:
            for attr in kwargs:
                setattr(schedule, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return schedule

    def remove_schedule(self, pk):
        """ Deletes an existing Schedule from the database.

        Arguments
        ---------
        pk: str
            the primary key of Schedule used in our table
        """
        schedule = self.find_schedule(pk)
        # delete all associated schedule_items too
        for ps in schedule.production_slots:
            self.remove_production_slot(ps.pk)
        self.db.delete(schedule)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)


    # PRODUCTION_SLOTS --------------------------------------------------------
    @property
    def production_slots(self):
        """ A list of all production_slots in the database."""
        return self.db.query(ProductionSlot).all()

    def add_production_slot(self, **kwargs):
        """ Add a new production_slot to the database.

        Arguments
        ---------
        `**kwargs` : dict
            other keyword arguments to the
            :class:`BruggCablesKTI.db.model.ProductionSlot`

        Returns
        -------
        production_slot:
            :class:`BruggCablesKTI.db.model.ProductionSlot`
        """
        production_slot = ProductionSlot(**kwargs)
        self.db.add(production_slot)
        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
        return production_slot

    def find_production_slot(self, pk):
        """ Find a production_slot.

        Arguments
        ---------
        pk : string
            The primary key of the ProductionSlot used in our table.

        Returns
        -------
        production_slot: :class:`BruggCablesKTI.db.model.ProductionSlot`
        """
        try:
            production_slot = self.db.query(ProductionSlot)\
                .filter(ProductionSlot.pk == pk)\
                .one()
        except NoResultFound:
            raise MissingEntry("No such ProductionSlot: {}".format(pk))
        return production_slot

    def update_or_create_production_slot(self, pk=None, **kwargs):
        """ Update an existing production_slot, or create it if it doesn't
        exist.

        Arguments
        ---------
        pk: str
            the primary key of ProductionSlot used in our table
        `**kwargs`
            additional keyword arguments for the
            :class:`BruggCablesKTI.db.model.ProductionSlot`

        Returns
        -------
        production_slot: :class:`BruggCablesKTI.db.model.ProductionSlot`
        """
        try:
            production_slot = self.find_production_slot(pk)
        except MissingEntry:
            production_slot = self.add_production_slot(**kwargs)
        else:
            for attr in kwargs:
                setattr(production_slot, attr, kwargs[attr])
            try:
                self.db.commit()
            except (IntegrityError, FlushError) as e:
                self.db.rollback()
                raise InvalidEntry(*e.args)
        return production_slot

    def remove_production_slot(self, pk):
        """ Deletes an existing ProductionSlot from the database.

        Arguments
        ---------
        pk: str
            the primary key of ProductionSlot used in our table
        """
        production_slot = self.find_production_slot(pk)
        self.db.delete(production_slot)

        try:
            self.db.commit()
        except (IntegrityError, FlushError) as e:
            self.db.rollback()
            raise InvalidEntry(*e.args)
