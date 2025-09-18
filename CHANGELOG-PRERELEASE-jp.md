# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Fixed

### Changed

### Removed

### Security

### Deprecated

## [1.14.3] - [2025-09-18]

### Fixed
- [#1767] `Vertex Filter By Axis` のインスペクターが編集モードの状態で閉じられたら、
通常のギズモが消える問題を修正
- [#1766] アバターのレンダラーのスケール、回転、位置などが初期値からズレた場合、`Vertex Filter By Axis` のプレビューが正しく
  表示されない問題を修正

## [1.14.2] - [2025-09-17]

### Fixed
- [#1765] アニメーターパラメーターを Float に補正する際に、一部のParameter Driverが正しい動作をしない問題を修正

## [1.14.1] - [2025-09-15]

### Fixed

- [#1761] 一部のジェネリックアバターがアップロードできない問題を修正

## [1.14.0] - [2025-09-13]

## [1.14.0-rc.2] - [2025-09-11]

### Added
- [#1743] `VF By Mask` が、Read-Writeがついていないマスクテクスチャにも対応しました。
- [#1755] Mask Texture Editor が導入されていない場合、`VF By Mask`のインスペクターにヒントを表示

## [1.14.0-rc.1] - [2025-09-09]

### Added
- [#1743] `VF By Mask` が、Read-Writeがついていないマスクテクスチャにも対応しました。

### Fixed

- [#1753] `Vertex Filter By Axis` と `Scale Adjuster` を併用する場合、プレビューとビルドの結果が異なる問題を修正
- [#1750] `Head Chop` コンポーネントを作りすぎて、ビルドの失敗につながる場合がある問題を修正
- [#1751] `Scale Adjuster` でボーンのスケールをゼロにした場合、例外が発生し、子オブジェクトの位置関係が保持できない問題を修正

## [1.14.0-rc.0] - [2025-09-07]

### Added
- [#1740] `VR By Shape` に、一つのコンポーネントで複数のブレンドシェープを選択できるように変更
  - 注意：以前のベータで作成されたデータとの互換性が損なわれます。 
- [#1738] `Mesh Cutter` に複数の頂点フィルターをつけた場合、そのどれか一つでも選択された頂点をすべて消す設定を追加

### Fixed
- [#1739] `Mesh Cutter` のプレビュー処理を軽量化

## [1.14.0-beta.2] - [2025-09-02]

### Fixed
- [#1726] 統合中にパラメーターの型が調整される場合、`Parameter Driver` が正しく動作しなくなる問題を修正
- [#1728] `Menu Item` そのものに Reactive Component がついてないものの、その子に RC がついている場合に正しく動作しないバグを修正
- [#1732] 完全に固定状態ののReactive ComponentがFXアニメーターより優先度が低い問題を修正

### Changed
- [#1729] `Set` 指定の `Shape Changer` が前の `Delete` 指定を上書きするように変更。1.13.xに入ってしまった、意図しない
  互換性のない仕様変更を元に戻す変更です。
- [#1732] 以前の Modular Avatar のバージョンでは、完全に固定された（常にアクティブ）なReactive Componentが、FXレイヤー
  より優先されませんでしたが、この仕様はバグです。このバージョンで修正し、常にFXレイヤーより優先されるようになったので、
  一部のアバターでは挙動が変わる可能性があります。

## [1.14.0-beta.1] - [2025-08-30]

### Fixed
- [#1721] 複数の `Shape Changer` や `Mesh Cutter` が同じメッシュに適用された場合、一部の頂点が正しく削除されない場合が
あるバグを修正

## [1.14.0-beta.0] - [2025-08-29]

### Added
- [#1667] `Mesh Cutter` を実装 - メッシュの一部を削除または非表示にできるコンポーネントです。
    - 頂点フィルター `By Bone`、`By Blendshape`, `By Axis` と `By Mask` (#1651) を実装
- [#1697] `ModularAvatarMergeArmature.GetBonesMapping` APIを公開
- [#1601] MA MMD Layer Control を使用時に WriteDefaults OFF のステートを含むレイヤーがある場合の警告を追加

### Fixed
- [#1670] 一部の場合、生成したメッシュがObjectRegistryに登録されないバグを修正
- [#1671] 一部のワールドにおいて、Shape ChangerがVRChatのクラッシュを引き起こす可能性がある問題を修正
- [#1679] `MA Shape Changer` の削除モードで、頭ボーン配下をルートボーンとして設定したメッシュが一人視点で表示されない問題を修正
- [#1682] `ModularAvatarMenuItem`操作時に`NullReferenceException `が発生する可能性がある問題を修正
  (@Tliks さんによる修正)
- [#1683] 頭ボーンの配下をルートボーンとして設定したレンダラーで、メッシュのバウンディングボックスがズレる問題を修正
  (@ReinaS-64892 さんによる修正)
- [#1675] レイヤー０になったレイヤーで、MMD Layer Controlを使ってMMD処理を受けるようにした場合、正しく動作しない問題を修正
- [#1704] インデックス形式が16ビットのメッシュから頂点を削除する際に例外が発生するバグを修正
- [#1713] 一部のメッシュが `Shape Changer` の削除モードで正しく処理されない問題を修正
- [#1715] `Shape Changer` や `Mesh Cutter` を使うと頂点色データがメッシュから完全に消される問題を修正
- [#1719] 直接インスペクターで値を変更した場合、`Scale Adjuster` が子オブジェクトの位置を調整しなかった問題を修正

### Changed
- [#1705] VRChat以外のプラットホームでも、リアクティブコンポーネントの初期状態を適用するように変更

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