﻿---
sidebar_position: 1
---

# リアクティブコンポーネント

リアクティブコンポーネントとは、付属しているGameObject、その親のアクティブ状態、そして[Menu Item](../menu-item.md)の選択状態に応じて何かを
引き起こすコンポーネントです。これにより、手動でアニメーションを設定することなく、簡単なトグルを構築したり、衣装の状態に応じてブレンドシェープを
変えたりすることができます。

現在利用可能なリアクティブコンポーネントは次の通りです:

- [Object Toggle](./object-toggle.md) - 他のゲームオブジェクトのアクティブ状態を制御します
- [Shape Changer](./shape-changer.md) - レンダラーのブレンドシェープを変更します

## リアクティブコンポーネントの一般的なルール

一般的に、リアクティブコンポーネントは起動状態ときに何らかの効果を適用します。リアクティブコンポーネントは次の条件が満たされるときに起動状態とみなされます:

- 付属しているGameObjectとその親がシーン階層でアクティブである
- リアクティブコンポーネントが[Menu Item](../menu-item.md)と同じGameObjectにある場合、Menu Itemが選択されている

アバターを構築した後、リアクティブコンポーネントは以下に反応します:

- GameObjectの状態を変更するアニメーション
- 他のリアクティブコンポーネントのアクティブ状態に影響を与えるObject Toggle
- Menu Itemの選択

### 優先ルール

同時に複数のリアクティブコンポーネントが起動状態であり、その効果が競合する場合(例: 1つがゲームオブジェクトをオフにしようとし、もう1つが
オンにしようとする場合)、階層の一番下にあるコンポーネントが優先されます。

### 反応タイミング

GameObjectのアクティブ状態の変更に反応するリアクティブコンポーネントは、1フレームの遅延後に反応します。ゲームオブジェクトが非アクティブになる場合、
ゲームオブジェクトの非アクティブ化は1フレーム遅れて行われ、同時に行われます。この理由については[Shape Changer](./shape-changer.md)
を
参照してください。

1つのリアクティブコンポーネントが他のリアクティブコンポーネントの状態を制御する場合、各リアクティブコンポーネントの発動に1フレームの遅延があります。遅延は
個々のリアクティブコンポーネントに適用され、A -> B -> Cのような構造でAがオフになる場合、タイミングは次のようになります:

- フレーム1: 何も起こらない(Aの無効化が遅延)
- フレーム2: Aが無効化される(Bの無効化が遅延)
- フレーム3: BとCが同時に無効化される

### プレビューシステム

リアクティブコンポーネントのメッシュ可視性への影響は、エディタのシーンビューに即座に反映されます。ただし、これにはいくつかの制限があります。特に、
オブジェクトの現在のアクティブ状態とMenu Itemの「デフォルト」状態を考慮しますが、Object Toggleが他のリアクティブコンポーネントに与える影響は考慮しません。
リアクティブコンポーネントの完全な効果を見るには、再生モードに入る必要があります。
