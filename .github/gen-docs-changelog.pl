#!/usr/bin/perl

use strict;
use warnings;

# We want to skip two sections - the main header, then up to the first version header.
# In a prerelease, we only want to skip the first section (not including the unreleased header)

if ($ENV{PRERELEASE} eq 'false') {
    while (<>) {
        if (/^\## /) { last; }
    }
}


while (<>) {
    if (/^## /) { print; last; }
}

while (<>) {
    print;
}