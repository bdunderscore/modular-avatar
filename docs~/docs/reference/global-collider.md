# Global Collider

The `MA Global Collider` component is used to define colliders on your avatar which can globally interact with other
avatars.

![Global Collider](global-collider.png)

## When should I use it?

You should use this component when you want to make a gimmick that can interact with the dynamics (e.g. VRChat
PhysBones)
of other avatars.

### Example Usage

- Adding a collider to a gimmick or prop so it can interact with PhysBones on other avatars.
- Moving a VRChat Hand collider to change where you grab things from. (Such as to the mouth to mimic biting)
- Adding shockwave/recoil effects to weaponry by animating the GameObject object with a collider.

## When should I not use it?

Because the number of global colliders is limited on some platforms (and on others, it may come with a performance
penalty),
you should use this component sparingly. In particular, on VRChat, using more than 6 global colliders will overwrite
your index
finger collider.

## What does it do?

This collider directs Modular Avatar to create a "global collider" for the transform it is attached to. A global
collider
is a collider that can interact with other avatars. On VRChat, this normally is only possible with certain built-in
colliders such as the fingers.

You can place this component on any game object and define its shape like a standard capsule or VRChat Physbone collider.

The implementation strategy may differ by platform. On VRChat, the number of global colliders is limited, so this
component will take over one of the base colliders (fingers) in order to implement the global collider.

On other platforms, we may implement this in a different way.

## Manual Remap

On VRChat, this toggle enables manually defining which collider MA Global Collider will hijack from the avatar.
It should be noted that not all colliders listed provide physics collisions with other avatars' PhysBones in VRChat.
The Head, Torso, and Feet are only contact senders.

## Low Priority Collider

Only available when Manual Remap is enabled. Any Global Collider with this will be treated as replacable. If another
Global Collider remaps the same Collider, the low priority one will be overwritten without warning.