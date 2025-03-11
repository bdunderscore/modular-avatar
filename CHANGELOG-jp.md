# Changelog

Modular Avatarの主な変更点をこのファイルで記録しています。
なお、プレリリース版の変更点は `CHANGELOG-PRERELEASE.md` に記録されます。

この形式は [Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) に基づいており、
このプロジェクトは [Semantic Versioning](https://semver.org/lang/ja/) に従っています。

## [Unreleased]

### Added
- CHANGELOGファイルを追加
- [#1482] `Merge Animator` に既存のアニメーターコントローラーを置き換える機能を追加

### Fixed
- [#1460] パラメーターアセットをMA Parametersにインポートするとき、ローカルのみのパラメーターが間違ってアニメーターのみ扱いになる問題を修正
 
### Changed
- [#1476] ModularAvatarMergeAnimator と ModularAvatarMergeParameter を新しい NDMF API (`IVirtualizeMotion` と `IVirtualizeAnimatorController`) を使用するように変更

### Removed

### Security

### Deprecated

## それより前

GitHubのリリースページをご確認ください: https://github.com/bdunderscore/modular-avatar/releases