---
sidebar_position: 10
---

# 扩展 Modular Avatar

Modular Avatar 可以使用 [NDM Framework](https://github.com/bdunderscore/ndmf) 进行扩展。使用 NDMF，你可以安排你的代码在 Modular Avatar 处理之前或之后运行。一般来说，如果你打算生成 Modular Avatar 组件，最好在 **Generating** 阶段执行你的代码，像这样：

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

未来，我们将提供更多 API 以实现与 Modular Avatar 的深度集成。如果你有特定的功能需求，请在 [Github](https://github.com/bdunderscore/modular-avatar/issues)上创建一个 issue。
