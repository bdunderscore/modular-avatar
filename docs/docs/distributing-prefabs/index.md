---
sidebar_position: 5
sidebar_label: Distributing Prefabs
---
import ReactPlayer from 'react-player'

# Distributing Prefabs

Modular Avatar is designed to make things easier for prefab authors, by allowing them to distribute prefabs which can be installed by simply dragging and dropping onto the avatar.
Here are some recommendations for how to best structure prefabs based on Modular Avatar.

## Guide your users to the official distribution point for Modular Avatar

Including a copy of Modular Avatar in your prefab's distribution is permitted by the license. However, this could result in users installing a very old version, or even accidentally downgrading and breaking their other prefabs.
I strongly recommend guiding users to the official distribution point for Modular Avatar, which is the [Modular Avatar GitHub repository](https://github.com/bdunderscore/modular-avatar/releases).

In the future, I intend to provide a VCC-based installation method, which should make things even easier. This is waiting on improvements to VCC itself, however.

## Use nested prefabs for compatibility with non-ModularAvatar setups

If you add a Modular Avatar component to a prefab, users will be unable to use that prefab without installing Modular Avatar.
Some users might prefer not to use Modular Avatar for whatever reason. If you wish to support this, you can use nested prefabs to separate out the core of your outfits from the Modular Avatar configuration.

If you've not worked with nested prefabs before, here's how to do this:

1. Create your outfit prefab as normal.
2. Open your prefab in prefab mode (double-click it in the project view). 
![Prefab mode](prefab-mode.png)
3. Drag the root of your prefab to the project window. When a window pops up, click Create Base. Rename the file to something you'll remember (e.g. `Outfit without Modular Avatar`)
<ReactPlayer playing muted loop playsinline url='/img/creating-base.mp4' />

Once you've done this, you can set up modular avatar components on the original prefab, and set any non-modular avatar settings on the base prefab you just created.
An easy way of applying such settings is using the prefab overrides menu - you can make changes in a test scene, then select which prefab to apply those changes to afterward.

![Apply as override](apply-as-override.png)

## Use internal parameters on your animator gimmicks

Using [internal parameters](../reference/parameters.md) can help avoid clashing with other prefabs. Internal parameters are automatically renamed to a unique name at build time, ensuring you won't have any name clashes.
