---
sidebar_position: 6
sidebar_label: FAQ
---

# FAQ

## Can Modular Avatar be installed using the VCC?

There is currently experimental support for installing using the VRChat Creator Companion.
You will first need to install the [VPM CLI tool](https://vcc.docs.vrchat.com/vpm/cli/), then run the following command:

```
vpm add repo https://vpm.nadena.dev/vpm.json
```

Close the VCC, if it's open already, then reopen it. You should now see a new package repository labeled `bd_` from which you can
install and update Modular Avatar.

To install prerelease versions, use this command instead (or in addition):

```
vpm add repo https://vpm.nadena.dev/vpm-prerelease.json
```

VCC installations will become the recommended method for installing Modular Avatar once the VCC supports adding custom repositories
from the GUI.

## Is it possible to use this to export to other formats, like VRM?

While Modular Avatar does not automatically apply its transformations when you e.g. export using UniVRM or other similar tools,
you can manually perform the Modular Avatar transformations first, and then VRM-ify the resulting normal avatar.

To do this, simply select the avatar, and from the Unity menu bar, select Tools -> Modular Avatar -> Manual bake avatar.
A copy of your avatar will be created with all Modular Avatar transformations applied. After that you can use e.g. UniVRM as usual.

:::caution

When you manually bake your avatar, Modular Avatar will generate a bunch of generated meshes and other assets, and they won't be cleaned up automatically.
These assets will be placed in a folder named `ModularAvatarOutput`. Once you're done with the manually-baked avatar you can feel free to delete them.

:::

## Is it possible to use Modular Avatar to merge outfits not designed for my specific avatar?

Yes, kind of. Modular Avatar assumes that bones are named the same way between the original avatar and the outfit. If this is not the case, you'll need to rename the outfit's bones to match up.

Once you do so, however, you can adjust the position of the outfit's bones, and those adjustments will be preserved when Merge Armature runs.