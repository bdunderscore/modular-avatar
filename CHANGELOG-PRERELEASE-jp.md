# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- [#1651] `MA Delete Mesh By Mask` を実装
- [#1677] 简体字ドキュメント翻訳の追加

### Fixed

### Changed

### Removed

### Security

### Deprecated

## [1.13.1] - [2025-08-02]

### Fixed
- [#1653] `Blendshape Sync`によりシーンが常に再描画されていた問題を修正
- [#1660] 削除されたブレンどシェイプが、VRChatのセーフティ設定によってアニメーションがブロックされているときに適用されない問題を修正

## [1.13.0] - [2025-07-12]

## [1.13.0-rc.1] - [2025-07-10]

### Added
- [#1642] `MA Material Swap` にクイックスワップモードを追加
- [#1635] `ModularAvatarMenuItem` にAPIを追加し、VRChat以外のプラットフォームで Menu Item を生成できるようにしました。

### Fixed
- [#1640] `MA Material Swap`が一部の状況で動作しない問題を修正
- [#1641] Material Swap の監視を修正
- [#1641] Material Swap にマテリアルをドラッグした際、マテリアルの置換先が空になるのを修正

## [1.13.0-rc.0] - [2025-07-06]

### Added
- [#1596]MA Rename Collision Tags コンポーネントを追加
  - Contacts のタグ（Collision Tags）を MA Parameters の自動リネームのようにユニークな名前にリネームできるようになりました

### Fixed
- [#1634] プロジェクトにVRCSDKが存在しない場合のコンパイルエラーを修正

### Changed
- [#1636] `Write Defaults設定に合わせる` モードでは、`Merge Animator` がブレンドツリーのみを含むレイヤーに対して、
　Direct Blend Treesが含まれていない場合はWrite Defaultsを強制的にONにしなくなりました。

## [1.13.0-beta.1] - [2025-07-03]

### Added
- [#1610] `Shape Changer` にしきい値設定を追加
- [#1629] Merge Animatorで統合されるアニメーターに破綻したレイヤー（ステートマシンが存在しないなど）を持つ場合、エラーを報告してビルドを続行するように変更

### Fixed
- [#1632] `Blendshape Sync` が無効化されたオブジェクト上でエディターで動作しない問題を修正
- [#1633] `Blendshape Sync` がビルド時にアバターの初期状態に正しく適用されない問題を修正

### Changed
- [#1608] [#1610] Shape Changerが、アニメーションされても完全に消す仕様になりました

## [1.13.0-beta.0] - [2025-06-21]

### Added
- [#1594] MA Informationで超過したパラメーター量も表示
- [#1604] `MA Material Swap` を実装
- [#1620] `MA Material Swap` の Inspector にマテリアル選択ボタンを追加
- [#1623] `MA Platform Filter` を実装

### Fixed
- [#1587] Mesh SettingsのGizmoが `親で指定されている時は継承、それ以外では設定` 設定のときに表示されない問題を修正
- [#1589] Merge AnimatorやMerge Motionコンポーネントのターゲットがnullの場合に `KeyNotFoundException` が発生する問題を修正
- [#1605] 複数の Material Setter が競合したときのプレビューがビルド結果と異なる問題を修正

## [1.13.0-alpha.2] - [2025-04-14]

### Fixed
- [#1558] Merge AnimatorでベースアバターのArmature内のTransformをアニメーションさせると壊れる問題を修正

## [1.13.0-alpha.1] - [2025-04-10]

### Fixed
- [#1552] Merge Blend Treeにて、メインアバターFXレイヤーと同じ名前のintやboolパラメーターがBlend Treeに含まれている場合、
  パラメーター型が修正されない問題を修正
- [#1553] リアクティブコンポーネントが生成するステートに、WD設定が正しくない問題を修正
- [#1555] VRC Animator Play Audioが、Audio Sourceまでの絶対パスで設定されている場合に、相対パスのMerge Animator
    コンポーネントとマージされた場合、指定されたオブジェクトが存在しないことを検出し、参照を絶対パスとして扱うように修正
  - 対象のパスにオブジェクトがある場合は、相対パスとして扱われます。安定性向上のためMerge Animatorコンポーネントと同じ
  　指定方法を使用することをお勧めします。

### Changed
- [#1551] Merge Animatorは、遷移のない単一のstateを持つブレンドツリーのレイヤーに対して常にWDをONに設定します。
  - 一部、以前の挙動に依存したアセットとの互換性を向上させるための変更です。

## [1.13.0-alpha.0] - [2025-04-08]

### Added
- (実験的機能) VRC以外のプラットフォームのサポートを有効化

## [1.12.3] - [2025-04-05]

### Fixed
- Additiveレイヤーの問題を修正（NDMFバージョンアップグレードによって修正）

### Changed
- [#1542] Merge Animatorは、アニメーションクリップを含む単一のstateを持つレイヤーに対してWD設定を一致させるが、
　    ブレンドツリーを含む場合は一致させないように変更されました。
  - これにより、1.12で導入された互換性の問題が修正されます（1.12.0では、単一のstateアニメーションクリップに対してWD設定
    と一致しないように変更されました）。

## [1.12.2] - [2025-04-03]

### Fixed
- [#1537] アニメーターパラメーターをアニメーションさせるカーブが、`Merge Motion` コンポーネントを使用して追加された場合、
  `Rename Parameters` によって更新されない問題を修正``

## [1.12.1] - [2025-04-02]

### Fixed
- [#1532] Modular Avatarが新しく作成したプロジェクトでコンパイラエラーを出す問題を修正

## [1.12.0] - [2025-04-01]

### Fixed
- [#1531] lylicalInventoryとの互換性問題を修正

### Changed
- [#1530] `MA Menu Item`の自動パラメーター機能も、オブジェクトのパスに基づいて名前を割り当てるようになりました。

## [1.12.0-rc.1] - [2025-03-28]

### Added
- [#1524] MMDワールド対応をアバター全体で無効にする機能を追加

### Fixed
- [#1522] `Convert Constraints` がアニメーション参照を変換できない問題を修正
- [#1528] `Merge Animator` が `アバターのWrite Defaults設定に合わせる` 設定を無視し、常に合わせてしまう問題を修正

### Changed
- [#1529] `MA Parameters` の自動リネームは、オブジェクトのパスに基づいて新しい名前を割り当てるように変更されました。これにより、
  `MA Sync Parameter Sequence` との互換性が向上します。
  - `MA Sync Parameter Sequence` を使用している場合は、このバージョンに更新した後、SyncedParamsアセットを空にして、
    すべてのプラットフォームを再アップロードすることをお勧めします。

## [1.12.0-rc.0] - [2025-03-22]

### Fixed
- [#1508] テクスチャのサイズが4の倍数でない場合に、エクスプレッションメニューアイコンの自動圧縮が失敗する問題を修正
- [#1513] iOSビルドでエクスプレッションメニューアイコンの圧縮が壊れる問題を修正

### Changed
- [#1514] `Merge Blend Tree` は `Merge Motion (Blend Tree)` に改名され、アニメーションクリップにも対応するようになりました

## [1.12.0-beta.0] - [2025-03-17]

### Added
- [#1497] CHANGELOGをドキュメンテーションサイトに追加
- [#1482] `Merge Animator` に既存のアニメーターコントローラーを置き換える機能を追加
- [#1481] [World Scale Object](https://m-a.nadena.dev/dev/ja/docs/reference/world-scale-object)を追加
- [#1489] [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)を追加

### Fixed
- [#1492] 前回のプレリリースでアイコンとロゴアセットが間違っていた問題を修正
- [#1501] MA Parametersコンポーネントのテキスト入力欄を編集する際にUnityのキーボードショートカットが機能しない問題を修正
- [#1410] 同期レイヤー内のモーションオーバーライドがBone Proxy/Merge Armatureオブジェクトの移動に対して更新されない問題を修正
- [#1504] 一部の状況で内部の`DelayDisable`レイヤーが不要なオブジェクトを参照しないように変更
  - これにより、オブジェクトがアニメーションされているかどうかを追跡するAAOなどのツールとの互換性が向上します

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
- [#1502] `World Fixed Object` は `VRCParentConstraint` を使用するようになり、Androidビルドで使用可能になりました。

## [1.12.0-alpha.2] - [2025-03-10]

### Added
- Added CHANGELOG files

### Changed
- [#1476] ModularAvatarMergeAnimator と ModularAvatarMergeParameter を新しい NDMF API (`IVirtualizeMotion` と `IVirtualizeAnimatorController`) を使用するように変更

## Older versions

Please see CHANGELOG.md