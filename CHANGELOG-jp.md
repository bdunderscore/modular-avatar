# Changelog

Modular Avatarの主な変更点をこのファイルで記録しています。
なお、プレリリース版の変更点は `CHANGELOG-PRERELEASE.md` に記録されます。

この形式は [Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) に基づいており、
このプロジェクトは [Semantic Versioning](https://semver.org/lang/ja/) に従っています。

## [Unreleased]

### Added

### Fixed

### Changed

### Removed

### Security

### Deprecated

## [1.12.1] - [2025-04-02]

### Fixed
- [#1532] Modular Avatarが新しく作成したプロジェクトでコンパイラエラーを出す問題を修正

## [1.12.0] - [2025-04-01]

### Added
- CHANGELOGファイルを追加
- [#1482] `Merge Animator` に既存のアニメーターコントローラーを置き換える機能を追加
- [#1481] [World Scale Object](https://m-a.nadena.dev/ja/docs/reference/world-scale-object)を追加
- [#1489] [`MA MMD Layer Control`](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)を追加

### Fixed
- [#1460] パラメーターアセットをMA Parametersにインポートするとき、ローカルのみのパラメーターが間違ってアニメーターのみ扱いになる問題を修正
- [#1489] `Merge Blend Tree` やリアクティブコンポーネントとMMDワールドの互換性の問題を修正。
  - 詳細は[ドキュメント](https://modular-avatar.nadena.dev/docs/general-behavior/mmd)を参照してください。
  - この動作を無効にするには、新しい `MA VRChat Settings` コンポーネントをアバターの適当なところに追加して、適切な設定を無効にしてください。
- [#1501] MA Parametersコンポーネントのテキスト入力欄を編集する際にUnityのキーボードショートカットが機能しない問題を修正
- [#1410] 同期レイヤー内のモーションオーバーライドがBone Proxy/Merge Armatureオブジェクトの移動に対して更新されない問題を修正
- [#1504] 一部の状況で内部の`DelayDisable`レイヤーが不要なオブジェクトを参照しないように変更
  - これにより、オブジェクトがアニメーションされているかどうかを追跡するAAOなどのツールとの互換性が向上します
- [#1508] テクスチャのサイズが4の倍数でない場合に、エクスプレッションメニューアイコンの自動圧縮が失敗する問題を修正
- [#1513] iOSビルドでエクスプレッションメニューアイコンの圧縮処理が壊れる問題を修正

### Changed
- [#1529] `MA Parameters` の自動リネームと `MA Menu Item` の自動パラメーター機能は、オブジェクトのパスに基づいて名前
  を割り当てるように変更されました。
  - `MA Sync Parameter Sequence` を使用している場合は、このバージョンに更新した後、SyncedParamsアセットを空にして、
    すべてのプラットフォームを再アップロードすることをお勧めします。
- [#1514] `Merge Blend Tree` は `Merge Motion (Blend Tree)` に改名され、アニメーションクリップにも対応するようになりました
- [#1476] ModularAvatarMergeAnimator と ModularAvatarMergeParameter を新しい NDMF API (`IVirtualizeMotion` と `IVirtualizeAnimatorController`) を使用するように変更
- [#1483] Merge Animator の 「アバターの Write Defaults 設定に合わせる」設定では、Additiveなレイヤー、および単一Stateかつ遷移のないレイヤー
　に対してはWrite Defaultsを調整しないように変更。
- [#1429] Merge Armature は、特定の場合にPhysBoneに指定されたヒューマノイドボーンをマージできるようになりました。
  - 具体的には、子ヒューマノイドボーンがある場合はPhysBoneから除外される必要があります。
- [#1437] Create Toggle for Selectionにおいて、複数選択時時に必要に応じてサブメニューを生成し、子としてトグルを生成するように変更されました。
- [#1499] `Object Toggle`で制御される`Audio Source`がアニメーションブロックされたときに常にアクティブにならないように、
    アニメーションがブロックされたときにオーディオソースを無効にするように変更。
- [#1502] `World Fixed Object` は `VRCParentConstraint` を使用するようになり、Androidビルドで使用可能になりました。

## それより前

GitHubのリリースページをご確認ください: https://github.com/bdunderscore/modular-avatar/releases