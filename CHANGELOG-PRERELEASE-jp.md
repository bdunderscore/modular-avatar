﻿# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- [#1497] CHANGELOGをドキュメンテーションサイトに追加
- [#1482] `Merge Animator` に既存のアニメーターコントローラーを置き換える機能を追加
- [#1481] [World Scale Object](https://m-a.nadena.dev/dev/ja/docs/reference/world-scale-object)を追加
- [#1489] [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)を追加

### Fixed
- [#1492] 前回のプレリリースでアイコンとロゴアセットが間違っていた問題を修正

### Changed
- [#1483] Merge Animator の 「アバターの Write Defaults 設定に合わせる」設定では、Additiveなレイヤー、および単一Stateかつ遷移のないレイヤー
　に対してはWrite Defaultsを調整しないように変更。
- [#1429] Merge Armature は、特定の場合にPhysBoneに指定されたヒューマノイドボーンをマージできるようになりました。
  - 具体的には、子ヒューマノイドボーンがある場合はPhysBoneから除外される必要があります。
- [#1437] Create Toggle for Selectionにおいて、複数選択時時に必要に応じてサブメニューを生成し、子としてトグルを生成するように変更されました。
- [#1499] `Object Toggle`で制御される`Audio Source`がアニメーションブロックされたときに常にアクティブにならないように、
  アニメーションがブロックされたときにオーディオソースを無効にするように変更。
- [#1489] `Merge Blend Tree` やリアクティブコンポーネントとMMDワールドの互換性の問題を修正。
  詳細は[ドキュメント](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)を参照してください。

### Removed

### Security

### Deprecated

## [1.12.0-alpha.2] - [2025-03-10]

### Added
- Added CHANGELOG files

### Changed
- [#1476] ModularAvatarMergeAnimator と ModularAvatarMergeParameter を新しい NDMF API (`IVirtualizeMotion` と `IVirtualizeAnimatorController`) を使用するように変更

## Older versions

Please see CHANGELOG.md