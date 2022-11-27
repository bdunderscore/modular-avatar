# Versioning policy

Modular Avatar subscribes to [Semantic Versioning](https://semver.org/). This means that:

* When incompatible changes are made, the first component of the version number will change (e.g. 1.0.0 -> 2.0.0).
* When new features are added in a backwards compatible way, the second component of the version number will change (e.g. 1.0.0 -> 1.1.0). Prefabs made with e.g. 1.1.0 may not work on 1.0.0, but prefabs made with 1.0.0 will work with 1.1.0.
* When minor bugfixes and other changes that don't affect save format are made, the third component will change (1.0.0 -> 1.0.1). Unless the bug fix affects your prefab, generally using an older patch version should not be a large issue.

In general, using the latest version of modular avatar under the same major version (1.x.x) is recommended.