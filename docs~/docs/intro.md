---
sidebar_position: 1
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Modular Avatar

Modular Avatar is a suite of **non-destructive** tools for modularizing your avatars, and for distributing avatar
components.
With Modular Avatar, adding a new outfit or gimmick to your avatar is as easy as drag-and-drop!

Modular Avatar's features are packaged as individual components, which can be added as needed. You can opt-in to just the features
you want to use. It can automatically merge outfits onto your avatar, build an animator out of multiple components, and much more.

## Installation

The recommended way to install Modular Avatar is using ALCOM. Installation using the VRChat Creator Companion is also supported.

<Tabs>
<TabItem value="ALCOM" label="ALCOM" default>

If you are using [ALCOM](https://vrc-get.anatawa12.com/en/alcom/) to install Modular Avatar, follow these steps:

1. Open "Packages" and select "ADD REPOSITORY".
2. Enter the following values under "Enter information about the repository" and click "ADD REPOSITORY":
    * `https://vpm.nadena.dev/vpm.json`
3. Confirm the following details are displayed, then click "Add VPM Repository":
    * Repository Name: `bd_`
    * URL: The URL you entered.

Afterward, open "Manage Project" for your project, click the + next to Modular Avatar, and then click `APPLY`.

![ALCOM Install UI](alcom-install.png)

![ALCOM Install Confirm UI](alcom-install-confirm.png)

To update to the latest version, click the area with the version number and green arrow, then click `APPLY`.

![ALCOM Update UI](alcom-update.png)

</TabItem>

<TabItem value="VCC" label="VRChat Creator Companion">

If you are using the [VRChat Creator Companion](https://vrchat.com/home/download), click here:
* [Add Modular Avatar to VCC](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm.json)

Then, go to "manage project" for your project, and click the + next to Modular Avatar.

![VCC UI](vcc-install.png)

To update to the latest version, just click the green arrow in the "Latest Version" column.

</TabItem>

</Tabs>

## Prerelease versions {#betas}

:::warning

Due to bugs in VCC dependency resolution, please use ALCOM to install prerelease versions of Modular Avatar.

:::

To make prerelease versions available in ALCOM, in your ALCOM Settings window, enable `Show pre-release packages`.

![Pre-release settings](prerelease.png)

The documentation for the prerelease branch can be [found here](https://modular-avatar.nadena.dev/dev).

Prerelease versions may contain bugs, and/or may have compatibility breaks as development progresses. Please leave
feedback and bug reports at the [github issues page](https://github.com/bdunderscore/modular-avatar/issues).

## What to read next

Once you've installed MA, check out one of the tutorials below:

* [Simple clothing setup](/docs/tutorials/clothing)
* [Advanced clothing setup](/docs/tutorials/adv_clothing)
* [Toggle an object](/docs/tutorials/object_toggle/)
* [Edit menus](/docs/tutorials/menu/)