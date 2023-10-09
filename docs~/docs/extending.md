---
sidebar_position: 10
---

# Extending Modular Avatar

Modular Avatar can be extended by using [NDM Framework](https://github.com/bdunderscore/ndmf). Using NDMF, you can 
arrange for your code to be run before or after Modular Avatar processing. Generally speaking, if you intend to generate
Modular Avatar components, it's best to execute your code in the Generating phase, like so:

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