# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- [#1497] Added changelog to docs site
- [#1482] Added support for replacing pre-existing animator controllers to `Merge Animator`
- [#1481] Added [World Scale Object](https://m-a.nadena.dev/dev/docs/reference/world-scale-object)
- [#1489] Added [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)

### Fixed
- [#1492] Fixed incorrect icon and logo assets in prior prerelease
- [#1489] Fixed compatibility issues between `Merge Blend Tree` or reactive components and MMD worlds.
  See [documentation](https://modular-avatar.nadena.dev/docs/general-behavior/mmd) for details on the new handling.

### Changed
- [#1483] The Merge Animator "Match Avatar Write Defaults" option will no longer adjust write defaults on states in
  additive layers, or layers with only one state and no transitions.
- [#1429] Merge Armature will now allow you to merge humanoid bones with PhysBones attached in certain cases.
    - Specifically, child humanoid bones (if there are any) must be excluded from all attached Physbones.
- [#1437] Create Toggle for Selection now creates submenus as necessary when multiple items are selected, and creates toggles as children.

### Removed

### Security

### Deprecated

## [1.12.0-alpha.2] - [2025-03-10]

### Added
- Added CHANGELOG files

### Changed
- [#1476] Switch ModularAvatarMergeAnimator and ModularAvatarMergeParameter to use new NDMF APIs (`IVirtualizeMotion` and `IVirtualizeAnimatorController`)

## Older versions

Please see CHANGELOG.md