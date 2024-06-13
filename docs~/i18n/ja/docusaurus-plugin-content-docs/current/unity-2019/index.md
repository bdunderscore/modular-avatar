---
sidebar_position: 11
---

# Unity 2019 対応について

Modular Avatar はUnity 2019の対応終了を視野に入れています。現段階ではModular AvatarをUnity 2019で利用できますが、新機能が使えない
場合があります。今後のバージョンでサポートを終了する予定です。終了時期は未定ですが、1.11.0あたりになる可能性が高いです。

## Unity 2019での違い

以下の機能は Unity 2019 と Unity 2022 で挙動が違います。

* 2022では、[MA Parameters](/ja/docs/reference/parameters)のUIを更新しています。
  [旧UIのドキュメンテーションページはこちらです。](old-parameters.md)
  * Unity 2019ではパラメーターのデフォルト値がゼロの場合、未設定扱いとなります。
    そのため、オーバーライドとしてゼロを設定することができません。 
