
import numpy as np
from matplotlib import pyplot as plt


def margin_histogram(df, offer_kind='all'):
    fig, ax = plt.subplots(figsize=(8, 6))
    bins = np.logspace(1, np.ceil(np.log(df.margin.max())), 50)

    if offer_kind in [None, 'all', 'both', ]:
        _ = ax.hist((df['margin'].where(df['kind'] == 'offer')).dropna(), histtype='step', bins=bins, color='blue', lw=2, label='offer')
        _ = ax.hist((df['margin'].where(df['kind'] == 'project')).dropna(), histtype='step', bins=bins, color='red', lw=2, label='project')
    else:
        _ = ax.hist((df['margin'].where(df['kind'] == offer_kind)).dropna(), histtype='step', bins=bins, color='blue', lw=2)

    _ = ax.legend()
    _ = ax.set_xlabel('Profit Margin')
    _ = ax.set_ylabel('Number of offers')
    _ = ax.set_xscale('log')
    _ = ax.set_title('Opportunity Margin Distribution')
    return fig, ax

def revenue_histogram(df, offer_kind='all'):
    fig, ax = plt.subplots(figsize=(8, 6))
    bins = np.logspace(1, np.ceil(np.log(df.revenue.max())), 50)

    if offer_kind in [None, 'all', 'both', ]:
        _ = ax.hist((df['revenue'].where(df['kind'] == 'offer')).dropna(), histtype='step', bins=bins, color='blue', lw=2, label='offer')
        _ = ax.hist((df['revenue'].where(df['kind'] == 'project')).dropna(), histtype='step', bins=bins, color='red', lw=2, label='project')
    else:
        _ = ax.hist((df['revenue'].where(df['kind'] == offer_kind)).dropna(), histtype='step', bins=bins, color='blue', lw=2)

    _ = ax.legend()
    _ = ax.set_xlabel('Revenue')
    _ = ax.set_ylabel('Number of offers')
    _ = ax.set_xscale('log')
    _ = ax.set_title('Opportunity Margin Distribution')
    return fig, ax


def cable_voltage_histogram(df):
    fig, ax = plt.subplots(figsize=(8, 6))
    for ck in [None, 'SEG', 'RMV', ]:
        _ = ax.hist((df['voltage'].where(df['kind'] == ck)).dropna(), histtype='step', bins=20, lw=2, label=ck if not None else 'None')
    _ = ax.legend()
    _ = ax.set_xlabel('Voltage')
    _ = ax.set_ylabel('Number of cables')
    _ = ax.set_title('Cable Voltage Distribution')
    return fig, ax


def cable_length_histogram(df):
    fig, ax = plt.subplots(figsize=(8, 6))
    for ck in [None, 'SEG', 'RMV', ]:
        _ = ax.hist((df['length'].where(df['kind'] == ck)).dropna(), histtype='step', bins=20, lw=2, label=ck if not None else 'None')
    _ = ax.legend()
    _ = ax.set_xlabel('Voltage')
    _ = ax.set_ylabel('Number of cables')
    _ = ax.set_title('Cable Length Distribution')
    return fig, ax
