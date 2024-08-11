---
sidebar_position: 3
---

# 簡単なON/OFFトグル設定

このチュートリアルでは、Modular Avatar の Reactive Object システムを使用してオブジェクトをON/OFFする簡単なメニューアイテムを作成します。

Anon-chan のフードをON/OFFしてみましょう。

![フード付きのあのんちゃん](0-initial.png)

まず、アバターを右クリックし、`Modular Avatar -> Create Toggle` を選択します。

![Create Toggle](1-menu.png)

すると、新しい GameObject がアバターの子として作成され、`Menu Item`、`Menu Installer`、`Object Toggle` コンポーネントが含まれます。

![コンポーネント類の初期状態](2-created.png)

`Object Toggle` で、`+` ボタンをクリックして新しいエントリを追加します。トグルしたいオブジェクトを空の欄にドラッグします。
このメニューアイテムでフードをOFFにしたいので、チェックボックスは空のままにします。

![完成！](3-configured.png)

これで設定完了です！トグルを試すには、メニューアイテムの `Default` ボックスをクリックしてください。フードが消えます。

![消えたフード](4-default-toggle.png)