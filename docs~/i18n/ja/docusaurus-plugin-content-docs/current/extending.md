---
sidebar_position: 10
---

# Modular Avatarを拡張する

Modular Avatar自体は[NDM Framework](https://github.com/bdunderscore/ndmf)で拡張できます。NDMFを使うことで、Modular Avatar
の処理の前後に実行するように設定できます。Modular Avatarコンポーネントを生成する場合はGeneratingフェーズで実行することをお勧めします。
たとえば、

```csharp

[assembly: ExportsPlugin(typeof(SetViewpointPlugin))]

namespace nadena.dev.ndmf.sample
{
    public class MyPlugin : Plugin<MyPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Do something", ctx => { /* ... */ });
        }
    }
}

```

In the future, additional APIs will be made available for deeper integration into Modular Avatar. If you have specific
functionality that you want, please create an issue on [our github](https://github.com/bdunderscore/modular-avatar/issues).

今後、Modular Avatarにより深く拡張するための追加のAPIが提供される予定です。特定の機能が必要な場合は、
[github](https://github.com/bdunderscore/modular-avatar/issues)にてissueを作成してください。