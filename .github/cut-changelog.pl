#!/usr/bin/perl

use strict;
use warnings;

my ($changelog_file, $header_file, $version, $excerpt_file) = @ARGV;

open my $changelog, '<', $changelog_file or die "Can't open $changelog_file: $!";
open my $header, '<', $header_file or die "Can't open $header_file: $!";
open my $new_changelog, '>', "$changelog_file.new" or die "Can't open $changelog_file.new: $!";

if (!$excerpt_file) {
    $excerpt_file = '/dev/null';
}

open my $excerpt, '>', $excerpt_file or die "Can't open $excerpt_file: $!";

# Copy all lines before the first "## "
while (my $line = <$changelog>) {
    last if $line =~ /^## /;
    print $new_changelog $line;
}

# Copy header into the output changelog
while (my $line = <$header>) {
    print $new_changelog $line;
}

# Generate new header: ## [version] - [YYYY-mm-DD]

my $date = `date +%Y-%m-%d`;
chomp $date;

print $new_changelog "## [$version] - [$date]\n";

# Copy all lines until the next ## into both the new changelog and $excerpt.
# Prune any ###-sections that contain no content

my @buffered;

while (my $line = <$changelog>) {
    if ($line =~ /^### /) {
        @buffered = ($line);
    } elsif ($line =~ /^\s*$/) {
        if (@buffered) {
          push @buffered, $line;
        } else {
            print $new_changelog $line;
            print $excerpt $line;
        }
    } elsif ($line =~ /^## /) {
        @buffered = ();
        print $new_changelog $line;
        last;
    } else {
        for my $buffered_line (@buffered){
            print $new_changelog $buffered_line;
            print $excerpt $buffered_line;
        }
        @buffered = ();
        print $new_changelog $line; 
        print $excerpt $line;
    }
}

# Copy remainder of changelog into new changelog
while (my $line = <$changelog>) {
    print $new_changelog $line;
}

rename "$changelog_file.new", $changelog_file or die "Can't rename $changelog_file.new to $changelog_file: $!";
