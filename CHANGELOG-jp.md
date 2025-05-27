# Changelog

Modular Avatarの主な変更点をこのファイルで記録しています。
なお、プレリリース版の変更点は `CHANGELOG-PRERELEASE.md` に記録されます。

この形式は [Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) に基づいており、
このプロジェクトは [Semantic Versioning](https://semver.org/lang/ja/) に従っています。

## [Unreleased]

### Added
- (実験的機能) VRC以外のプラットフォームのサポートを有効化
- [#1594] MA Informationで超過したパラメーター量も表示
- `MA Material Swap` を実装
- [#1623] `MA Platform Filter` を実装
- [#1610] `Shape Changer` にしきい値設定を追加
- [#1629] Merge Animatorで統合されるアニメーターに破綻したレイヤー（ステートマシンが存在しないなど）を持つ場合、エラーを報告してビルドを続行するように変更
- [#1596] MA Rename Collision Tags コンポーネントを追加
  - Contacts のタグ（Collision Tags）を MA Parameters の「名前を変更」や「自動リネーム」と同様にリネームできるコンポーネントです。

### Fixed
- [#1587] Mesh SettingsのGizmoが `親で指定されている時は継承、それ以外では設定` 設定のときに表示されない問題を修正
- [#1589] Merge AnimatorやMerge Motionコンポーネントのターゲットがnullの場合に `KeyNotFoundException` が発生する問題を修正
- [#1605] 複数の Material Setter が競合したときのプレビューがビルド結果と異なる問題を修正
- [#1632] `Blendshape Sync` が無効化されたオブジェクト上でエディターで動作しない問題を修正
- [#1633] `Blendshape Sync` がビルド時にアバターの初期状態に正しく適用されない問題を修正
- [#1634] プロジェクトにVRCSDKが存在しない場合のコンパイルエラーを修正

### Changed
- [#1608] [#1610] Shape Changerが、アニメーションされても完全に消す仕様になりました

### Removed

### Security

### Deprecated

## [1.12.5] - [2025-04-14]

### Fixed
- [#1555] VRC Animator Play Audioが、Audio Sourceまでの絶対パスで設定されている場合に、相対パスのMerge Animator
  コンポーネントとマージされた場合、指定されたオブジェクトが存在しないことを検出し、参照を絶対パスとして扱うように修正
  - 対象のパスにオブジェクトがある場合は、相対パスとして扱われます。安定性向上のためMerge Animatorコンポーネントと同じ
  　指定方法を使用することをお勧めします。
- [#1558] Merge AnimatorでベースアバターのArmature内のTransformをアニメーションさせると壊れる問題を修正
- NDMFの依存バージョンを更新
  - VRChat Avatar Descriptor内のレイヤーが重複している場合、すべてのアニメーターコンテンツが無視される問題を修正
  - 起動時に発生する `NullReferenceException` を修正
  - AnimationIndex内の `NullReferenceException` を修正
  - アニメーションカーブのパスが複数回書き換えられると削除される問題を修正
  
## [1.12.4] - [2025-04-10]

### Fixed
- [#1552] Merge Blend Treeにて、メインアバターFXレイヤーと同じ名前のintやboolパラメーターがBlend Treeに含まれている場合、
  パラメーター型が修正されない問題を修正
- [#1553] リアクティブコンポーネントが生成するステートに、WD設定が正しくない問題を修正

### Changed
- [#1551] Merge Animatorは、遷移のない単一のstateを持つブレンドツリーのレイヤーに対して常にWDをONに設定します。
  - 一部、以前の挙動に依存したアセットとの互換性を向上させるための変更です。

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