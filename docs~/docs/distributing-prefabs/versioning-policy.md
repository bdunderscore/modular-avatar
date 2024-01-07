---
sidebar_position: 1
---

# Versioning policy

Modular Avatar subscribes to [Semantic Versioning](https://semver.org/). This means that:

* When incompatible changes are made, the first component of the version number will change (e.g. 1.0.0 -> 2.0.0).
* When new features are added in a backwards compatible way, the second component of the version number will change (e.g. 1.0.0 -> 1.1.0). Prefabs made with e.g. 1.1.0 may not work on 1.0.0, but prefabs made with 1.0.0 will work with 1.1.0.
* When minor bugfixes and other changes that don't affect save format are made, the third component will change (1.0.0 -> 1.0.1). Unless the bug fix affects your prefab, generally using an older patch version should not be a large issue.

In general, using the latest version of modular avatar under the same major version (1.x.x) is recommended.

## Internals and pass references

All `internal` class names and method names are not considered stable APIs and are subject to change at any time,
including patch releases (1.0.0 -> 1.0.1). In particular, the "qualified name" of NDMF passes is also not considered
API stable, nor is the order in which Modular Avatar passes will run.

If you have a use case for depending on specific Modular Avatar passes, please file a feature request with details of
your use case on the github, and I'll consider adding a stable API for adding those pass dependencies.