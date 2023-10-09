---
sidebar_position: 1
---

# Modular Avatar

Modular Avatar（モジュラーアバター）は、**非破壊的な**アバター製作用、そしてアバター部品の配布補助ツールの集まりです。
Modular Avatarを使えば、D&Dだけでアバターに新しい衣装やギミックを導入できます！

Modular Avatarの機能はそれぞれコンポーネントとして提供され、必要に応じて必要な機能だけ追加できます。自動的に衣装を統合したり、複数のアセットからアニメーターを構築したり、様々な面で製作を補助します。

## インストール

VRChat Creator CompanionでModular Avatarをインストールすることをお勧めします。VCCをインストールしたら、こちらをクリックしてください：
* [Modular AvatarをVCCに追加](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm.json)

その後、プロジェクトの"manage project"を開き、Modular Avatarの+をクリックしてください。

![VCC UI](vcc-install.png)

最新版にアップデートするには、"Latest Version"の緑色の矢印をクリックしてください。

インストール後は以下のチュートリアルを参照してください。
* [簡単な衣装設定](/docs/tutorials/clothing)
* [複雑な衣装設定](/docs/tutorials/adv_clothing)
* [アニメーターでトグル作成](/docs/tutorials/object_toggle/)
* [簡易メニュー作成](/docs/tutorials/menu/)

## 手動導入

unitypackageからModular Avatarを手動でインストールすることもできます。最新版のunitypackageは[GitHubリリースページ](https://github.com/bdunderscore/modular-avatar/releases).
にあります。アップデートする際には、Modular Avatarフォルダを削除する必要がある場合があります。

## テスト版

VCCでテスト版をインストールできるようにするには、[こちらをクリック](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm-prerelease.json)

Then, in your VCC Settings -> Packages window, uncheck the `bd_` repository, check the `bd_ prerelease` repository, and enable `Show pre-release packages`.

そして、VCCの設定画面のPackagesタブで、`bd_`のリポジトリをチェックを外し、`bd_ prerelease`のリポジトリをチェックを入れ、`Show pre-release packages`にチェックを入れてください。

![Pre-release settings](prerelease.png)

テスト版のドキュメントは[こちら](https://modular-avatar.nadena.dev/dev)にあります.

テスト版は開発中のため、バグがあったり、互換性のない変更を加える可能性があります。
バグ報告やフィードバックは[GitHubのissueページ](https://github.com/bdunderscore/modular-avatar/issues)へお願いします。