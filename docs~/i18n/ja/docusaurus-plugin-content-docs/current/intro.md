---
sidebar_position: 1
---

# Modular Avatar

Modular Avatar（モジュラーアバター）は、**非破壊的な**アバター製作用、そしてアバター部品の配布補助ツールの集まりです。
Modular Avatarを使えば、D&Dだけでアバターに新しい衣装やギミックを導入できます！

Modular Avatarの機能はそれぞれコンポーネントとして提供され、必要に応じて必要な機能だけ追加できます。自動的に衣装を統合したり、複数のアセットからアニメーターを構築したり、様々な面で製作を補助します。

## インストール

### VCCでのインストール

VRChat Creator CompanionでModular Avatarをインストールすることをお勧めします。VCCをインストールしたら、こちらをクリックしてください：

* [Modular AvatarをVCCに追加](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm.json)

その後、プロジェクトの"manage project"を開き、Modular Avatarの+をクリックしてください。

![VCC UI](vcc-install.png)

最新版にアップデートするには、"Latest Version"の緑色の矢印をクリックしてください。

### ALCOMでのインストール

[ALCOM](https://vrc-get.anatawa12.com/ja/alcom/)でModular Avatarのインストールを指定する場合があります。

インストール後、以下の手順でVPMリポジトリを追加してください。

* 「パッケージ管理」 > 「VPMリポジトリを追加」
* 「リポジトリ情報を入力」に次の値を入力し、「VPMリポジトリを追加」
  * `https://vpm.nadena.dev/vpm.json`
* 以下の情報が記載されることを確認し、「VPMリポジトリを追加」
  * リポジトリ名: `bd_`
  * URL: 入力したURL

その後、プロジェクトの"管理"を開き、Modular Avatarの+をクリックし、「適用」をクリックしてください。

![ALCOM Install UI](alcom-install.png)

![ALCOM Install Confirm UI](alcom-install-confirm.png)

最新版にアップデートするには、バージョン番号と緑色の矢印を囲った枠内をクリックし、「適用」をクリックしてください。

![ALCOM Update UI](alcom-update.png)

## テスト版

テスト版のドキュメントは[こちら](https://modular-avatar.nadena.dev/dev)にあります.

テスト版は開発中のため、バグがあったり、互換性のない変更を加える可能性があります。
バグ報告やフィードバックは[GitHubのissueページ](https://github.com/bdunderscore/modular-avatar/issues)へお願いします。

### VCCでのテスト版インストール

VCCでテスト版をインストールできるようにするには、[こちらをクリック](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm-prerelease.json)

そして、VCCの設定画面のPackagesタブで、`bd_`のリポジトリをチェックを外し、`bd_ prerelease`のリポジトリをチェックを入れ、`Show pre-release packages`にチェックを入れてください。

![Pre-release settings](prerelease.png)

### ALCOMでのテスト版インストール

[ALCOM](https://vrc-get.anatawa12.com/ja/alcom/)でModular Avatarのインストールを指定する場合があります。

インストール後、以下の手順でVPMリポジトリを追加してください。

* 「パッケージ管理」 > 「VPMリポジトリを追加」
* 「リポジトリ情報を入力」に次の値を入力し、「VPMリポジトリを追加」
  * `https://vpm.nadena.dev/vpm-prerelease.json`
* 以下の情報が記載されることを確認し、「VPMリポジトリを追加」
  * リポジトリ名: `bd_ (prereleases)`
  * URL: 入力したURL

VPMリポジトリの追加が完了したら、テスト版が表示されるように変更します。

* （現在「通常版」を使用している場合は）「パッケージ管理」タブで、`bd_`リポジトリのチェックを外し、`bd_ (prereleases)`リポジトリのチェックを入れてください。
* 「設定」タブで、 `VPMパッケージ` の中にある `プレリリース版のパッケージを表示する`にチェックを入れてください。

![ALCOM Change Repogitories UI](alcom-prerelease-repo.png)

![ALCOM enable Prerelease UI](alcom-prerelease-settings.png)

終わったら、プロジェクトの"管理"を開き、Modular Avatarの+をクリックし、「適用」をクリックしてください。

![ALCOM Install UI](alcom-install.png)

![ALCOM Install Confirm UI](alcom-install-confirm.png)

最新版にアップデートするには、バージョン番号と緑色の矢印を囲った枠内をクリックし、「適用」をクリックしてください。

![ALCOM Update UI](alcom-update.png)

## インストール後

インストール後は以下のチュートリアルを参照してください。

* [簡単な衣装設定](/docs/tutorials/clothing)
* [複雑な衣装設定](/docs/tutorials/adv_clothing)
* [アニメーターでトグル作成](/docs/tutorials/object_toggle/)
* [簡易メニュー作成](/docs/tutorials/menu/)
