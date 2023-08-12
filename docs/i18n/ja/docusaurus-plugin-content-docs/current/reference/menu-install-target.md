# Menu Install Target

Menu Install Targetコンポーネントは、[MA Menu Installer](menu-installer.md)コンポーネントの「メニューを選択」ボタンを実装するために使用されるコンポーネントです。
MA Menu Installerコンポーネントからメニューを「引っ張り」、自分の位置にインストールします。

![Menu Install Target](menu-install-target.png)

## いつ使うの？

[MA Menu Installer](menu-installer.md)コンポーネントの「メニューを選択」ボタンを使用すると、必要に応じてこのコンポーネントが作成されます。
ほとんどの場合、手動で作成する必要はありません。

## 何をするもの？

このコンポーネントは、選択されたメニューインストーラーの「インストール先」設定を上書きし、Menu Install Targetの位置にインストールさせます。
これでMenu Installerを仕様したアセットを[オブジェクト型メニューシステム](../tutorials/menu)に併用できるようになります。