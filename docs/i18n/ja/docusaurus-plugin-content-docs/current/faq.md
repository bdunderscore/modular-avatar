---
sidebar_position: 6
sidebar_label: FAQ
---

# FAQ

## VCCを使ってModular Avatarを導入できますか？

VRChat Creator Companionを使ってModular Avatarをインストールすることに当たって実験的に対応しています。
まずは[VPM CLIを導入して](https://vcc.docs.vrchat.com/vpm/cli/)から、以下のコマンドを実行してください。

```
vpm add repo https://vpm.nadena.dev/vpm.json
```

開いていればVCCを一度閉じて、再度開いてください。`bd_`というリポジトリーが追加されて、そこからModular Avatarをインストール・更新できます。

ベータ版の導入は以下のコマンドを実行してください。

```
vpm add repo https://vpm.nadena.dev/vpm-prerelease.json
```

GUIからカスタムリポジトリを追加する対応がVCCに追加されたら、推奨インストール方法とする予定です。

## VRMなど、他の形式へのエクスポートでも使えますか？

UniVRMなどを使った場合は自動的に変換しませんが、手動で変換して、それでできた普通のアバターをエクスポートすることがかのうです。

これをするには、まずはアバターを選択し、Unityのメニューバーアから「Tools -> Modular Avatar -> Manual bake avatar」を選択します。
すると、Modular Avatarの変換が適用されたアバターのコピーが作成されます。その後は普通にUniVRMなどを使えば良いです。

:::caution

手動ベイクの場合、Modular Avatarに生成されるメッシュ等が自動的に削除されず、何回か繰り返すと溜まります。
自動生成されるアセットは`ModularAvatarOutput`というフォルダーにまとめられますので、ベイクされたアバターは使い終わったら削除してもかまいません。

:::

## 非対応衣装の統合にも使えますか？

ある程度は可能です。Modular Avatarは、元のアバターと衣装のボーン名が同じであることを前提としています。
ボーン名が異なる場合はまず名前を合わせる作業が必要になります。

名前さえ合わせることができたら、衣装のボーンの位置などを調整することができます。Merge Armatureが実行されると、
その位置調整を保持します。