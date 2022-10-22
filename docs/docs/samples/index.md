---
sidebar_position: 3
sidebar_label: Samples
---

# Samples

Modular Avatar comes with a few sample prefabs to demonstrate its features. These can be found under "Packages -> Modular Avatar -> Samples" in your project window.

![Samples](wheretofind.png)

## Fingerpen

The fingerpen prefab is a useful pen that can be used anywhere.
Simply drop it onto your avatar, then click the "Select Menu" button in the menu installer component, and double-click the menu you want to install the fingerpen controls to.

The fingerpen prefab demonstrates:

* Installing animators using [Merge Animator](../reference/merge-animator.md)
* Automatically configuring [synced parameters](../reference/parameters.md)
* Setting up [menus](../reference/menu-installer.md).
* Using the [Bone Proxy](../reference/bone-proxy.md) component to place objects inside of the avatar's bones, in an avatar-agnostic way

## Clap

By dragging the Clap prefab onto your avatar, when you bring your hands together, you'll hear a clap sound effect, and some particles will spawn.
As with fingerpen, click the select menu button to select where to put the on/off switch.

In addition to everything demonstrated in the fingerpen prefab, the clap sample also shows off using internal parameters with Contact Receivers to avoid parameter clashes with other components.