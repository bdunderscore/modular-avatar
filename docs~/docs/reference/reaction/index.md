---
sidebar_position: 1
---

# Reactive Components

Reactive Components are components which apply some effect to your avatar based on the active state of their GameObject
(and its parents), or [Menu Item](../menu-item.md) enabled states. This allows you to build up simple toggles, adjust
blendshapes as you change your outfit, and more - without needing to manually set up any animations.

The following reactive components are currently available:

* [Object Toggle](./object-toggle.md) - controls the active state of other game objects
* [Shape Changer](./shape-changer.md) - modifies blendshapes on a target renderer
* [Material Setter](./material-setter.md) - changes materials on a target renderer

## General rules for reactive components

In general, reactive components apply some kind of effect when they are _active_. A reactive component is considered
active when:

- Its GameObject, and all parents, is active in the scene hierarchy.
- If the reactive object is on the same GameObject as, or a child of a [Menu Item](../menu-item.md), the Menu Item is
  selected.
  - Note that only the first parent Menu Item is considered (parent Submenus are ignored).

After building your avatar, reactive components respond to the following:

- Animations which change the state of GameObjects
- Object Toggles which influence the active state of other reactive components
- Menu Item selections

You may also select the "Invert condition" option; in this case, the effect of the component is applied when _any_ of
the above conditions is _not_ true.

### Priority rules

If multiple reactive components are active at the same time, and their effects conflict (e.g., one tries to turn off a
game object, one tries to turn on a game object), the component lowest in the hierarchy takes precedence.

### Reaction timing

:::warning

The precise timing of reactive component activation is subject to change in the future as we optimize the implementation
of reactive components. You should not rely on the exact timing of reactive components for complex effects.

:::

Reactive components responding to the change of a GameObject's active state will do so after a one frame delay. When the
game object is being deactivated, the game object's deactivation will be delayed by one frame to happen at the same
time.
See [Shape Changer](./shape-changer.md) for more information on why this is the case.

If one reactive component controls the state of another reactive component, then there will be a one frame delay between
each reactive component triggering. The delay will be applied to each reactive component individually, so if you have
A -> B -> C, and A is turned off, the timing will be as follows:

* Frame 1: Nothing happens (A's disable is delayed)
* Frame 2: A is disabled (B's disable is delayed)
* Frame 3: B and C are disabled at the same time.

### Debugging problems

The Reactive Components system includes a debugger which can be used to simulate the effect of toggling on/off various
objects or menu items on your avatar. To access it, right click a Game Object and choose
`Modular Avatar -> Show Reaction Debugger`. For a detailed description of how it works, see the
[debugger documentation](./debugger/index.md).