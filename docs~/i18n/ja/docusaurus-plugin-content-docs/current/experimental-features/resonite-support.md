# Resonite対応

Modular Avatarには、実験的な機能として、Resonite向けのアバターをビルドできます。
この機能を有効にするには、ALCOMを使用して「Modular Avatar - Resonite support」パッケージをインストールし、[実験的な機能のサポート](../experimental-features)を有効にする必要があります。

Resonite向けのアバターをビルドするには、NDMFコンソール (Tools -> NDM Framework -> NDMF Console)を開き、ウィンドウの上部でアバターを選択し、「Avatar platform」として「Resonite」を選択し、Buildをクリックしてください。
正常にビルドが完了すると、「Build finished!」というメッセージが表示されます。
その後、「copy to clipboard」をクリックして、resoniteでCtrl-Vを押すか、「Save as...」をクリックして、アバターを`resonitepackage`ファイルとして保存できます。

Resoniteのビルドプロセスは、ビルド中にアバターの特定の機能（ビジュアル、目の位置、物理ボーン/ダイナミックボーンなど）を自動的にコピーします。

## 現在対応している機能

| 機能 | 対応状況 | 制限事項 |
| ------- | --------- | ----------- |
| Avatar の視点位置 | ✅ | なし |
| Visemes（口パク） | 部分的 | ブレンドシェープ型のみ |
| 揺れもの設定 | 部分的 | 下記参照 |
| Reactive Components | ⌛ | 対応予定 |
| Unity Constraints | ⌛ | 対応予定 |
| 読み込み途中でのアバター表示への対策 | ✅ | None |

## 現在対応しているModular Avatarのコンポーネント

| コンポーネント | 対応状況 | 制限事項 |
| ------- | --------- | ----------- |
| Blendshape Sync | ⌛ | 対応予定 |
| Bone Proxy | ✅ | なし |
| Convert Constraints | ✖ | VRChat のみで対応 |
| Menu Group | ⌛ | 対応予定 |
| Menu Install Target | ⌛ | 対応予定 |
| Menu Installer | ⌛ | 対応予定 |
| Menu Item | ⌛ | 対応予定 |
| Merge Animator | ✖ | VRChat のみで対応 |
| Merge Armature | ✅ | なし |
| Merge Blend Tree | ✖ | VRChat のみで対応 |
| Mesh Settings | ⌛ | 対応予定 |
| MMD Layer Control | ✖ | VRChat のみで対応 |
| Move Independently | ✅ | なし |
| Parameters | ⌛ | 対応予定（DynVarとして実装する予定）|
| Physbone Blocker | ✅ | なし |
| Remove Vertex Color | ✅ | なし |
| Replace Object | ✅ | なし |
| Scale Adjuster | ✅ | なし |
| Sync Parameter Sequence | ✖ | VRChat のみで対応 |
| Visible Head Accessory | ⌛ | 対応予定 |
| VRChat Settings | ✖ | VRChat のみで対応 |
| World Fixed Object | ⌛ | 対応予定 |
| World Scale Object | ⌛ | 対応予定 |

## 揺れものについて

Modular Avatarは、[Portable Dynamic Bones](./portable-avatar-components#portable-dynamic-bones)またはVRChatのPhysBonesを使用して作成されたダイナミックボーンを検出し、コライダー設定を含めてResoniteのダイナミックボーンに変換しようとします。

Resoniteには独自のダイナミックボーンシステムがあるため、ほとんどの設定オプションは変換されません。ただし、除外（Physbone Blockersを含む）、コライダー、および衝突範囲は変換されます。

Dynamic Bonesは、ボーン名に基づいて、いくつかの名前付き「テンプレート」にグループ化されます。テンプレート名は、ポータブルダイナミックボーンコンポーネントにグループ名を指定することで上書きできます。
または、Resoniteで、`Avatar Settings` -> `Dynamic Bone Settings` スロットの下にあるオブジェクトをクローンし、新しいテンプレート名に設定し、ダイナミックボーンを定義したスロットの下にある`Template Name`スロットの名前を変更することで、新しいテンプレートを作成できます。

同じテンプレートの下にあるすべてのダイナミックボーンは、Inertia、InertiaForce、Damping、Elasticity、およびStiffnessの設定を共有します。これらの設定は、対象のダイナミックボーンのいずれかでも変更すればすべてが連動します。

## アバター設定ののコピー機能

Modular Avatarは、Resoniteアバターの異なるバージョン間でアバター設定をコピーするシステムを自動的に導入します。これにより、Resonite固有の設定（ダイナミックボーンの設定など）を設定し、Unityから再インポートした後に新しいバージョンのアバターにコピーできます。

具体的には、`Avatar Settings`スロットの下にあるすべてのスロットをコピーし、同じ名前のスロットがあれば上書きします。自分のスロットを`Avatar Settings`スロットに追加することもでき、これらもコピーされます。

設定をコピーするには、古いアバターをResoniteで着用し、新しいアバターをレーザーで持ちます。コンテキストメニューから`MA Settings Copier` -> `Copy To Avatar`を選択します。これにより、古いアバターの設定が新しいアバターにコピーされます。その後、新しいアバターを着用すると、設定が適用されます。

## 自動設定されるDynVar

Modular Avatarは、アバターシステムで使用できるいくつかのDynamic Variableを定義しています。

自動追加されるDynVarの仕様は、現在実験的なものも含まれるため、将来的に変更される可能性があります。

| 名前 | 型 | 詳細 |
| ---- | ---- | ----------- |
| `modular_avatar/AvatarRoot` | `Slot` | アバターのルートスロット（`CenteredRoot`の親） |
| `modular_avatar/AvatarWorn` | `bool` | アバターが現在着用されているかどうか（アバターがUserスロットの直下にある場合に検出） |
| `modular_avatar/AvatarSettingsRoot` | `Slot` | `Avatar Settings`オブジェクト |
| `modular_avatar/AvatarPoseNode.[type]` | `Slot` | | `[type]`の`AvatarPoseNode`コンポーネントを含むスロット（例：`Head Proxy`） |
| `modular_avatar/MeshNotLoaded` | `bool` | アバターのメッシュが読み込まれていないかどうか。なお、この変数は読み込み途中のメッシュがある場合「false」になり、ない場合は「未定義」になるので注意。この仕様は将来的に変更される可能性が高いのでご注意ください |
| `modular_avatar/BoneRef_[name]` | `Slot` | ヒューマノイドボーンを名前で参照します。名義は今後変更される可能性があります。 |
| `modular_avatar/BonePose_[name]` | `float4x4` | 該当するボーンの初期ポーズです。_名前・内容の調整が入る可能性が高い機能です。|

なお、ほかのギミック用に、アバタールートに「Avatar」のDynamic Variable Spaceも生成されます。

<!-- TODO: Screenshots -->