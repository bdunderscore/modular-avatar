---
sidebar_label: Rename VRChat Collision Tags
---

# Rename VRChat Collision Tags

`MA Rename Collision Tags`コンポーネントを使用すると、VRChat Contactsシステムで使用される接触判定用のタグを
名前変更して、他のコンポーネントやギミックとの名前の衝突を回避できます。

![Rename VRChat Collision Tags UI](rename-vrchat-collision-tags.png)

## いつ使用すべきですか？ {#when-should-i-use-it}

`MA Rename Collision Tags`コンポーネントは、他のユーザーのアバターと干渉しないようにしたい接触判定がある場合や、
他のコンポーネントやギミックとの名前の衝突を回避したい場合に使用します。
また、同じタグ名を使用して複数のギミックをあえて相互作用させる設定にするためにも使用できます。

## 使い方は？ {#how-do-i-use-it}

`MA Rename Collision Tags`コンポーネントをアバターの任意のGameObjectに配置し、名前を変更したいタグを追加します。
タグを「自動リネーム」に設定すると、Modular Avatarが自動的に衝突しない名前を選定します。
手動で名前を設定することもできます。同じGameObjectの下にあるContactsのタグは、それに応じて名前が変更されます。

このコンポーネントの階層スコープ内のAnimator controllerと、`MA Merge Motion`で使用するclipやBlend Treeなどの
単体Motionでは、`prefix_CollisionTag_[tag]`という名前になっているAnimator parameterも名前変更されます。
`prefix_CollisionTag_`の部分は保持され、`[tag]`には同じCollision Tagのマッピングが適用されます。これには自動リネームや、
ネストされた`MA Rename Collision Tags`コンポーネントによる名前変更も含まれます。parameter名には`_CollisionTag_`
区切り文字を1つだけ含める必要があります。2つ以上含む名前は曖昧なため、ビルドエラーが報告されます。

`MA Rename Collision Tags`コンポーネントを、Contactsの`Root Transform`の親GameObjectに配置しても、
そのContactsのタグは変更されませんので注意してください。

`MA Rename Collision Tags`コンポーネントを別の`MA Rename Collision Tags`コンポーネントの下にネストすることもできます。
この場合、タグは最初に子コンポーネントの設定に従って名前が変更され、その後親コンポーネントの設定が適用されます。
これにより、名前変更ルールの階層を作成し、より複雑なギミックを単純なコンポーネントから構築することができます。