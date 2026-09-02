#!/bin/sh

set -x

if echo $1 | grep '[a-z]-[0-9]' > /dev/null; then
  echo "Tag name does not follow semver prerelease syntax: $REF_NAME"
  exit 1
fi
