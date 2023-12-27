import ReactPlayer from 'react-player'

# Move Independently

<ReactPlayer controls muted loop playsinline url='/img/move-independently.mp4' />

The Move Independently component allows you to move an object without affecting its children.
This component has no effect at runtime; it is purely for use in the editor.

## When should I use it?

This component is intended to be used when adjusting the fit of outfits on your avatar. You can, for example,
adjust the position of the hips object of the outfit without impacting the position of other objects.

## Grouping objects

By checking boxes under the "Objects to move together" field, you can create a group of objects that move together.
For example, you might move the hips and upper leg objects together, but leave the lower leg objects behind.

## Limitations

While this component supports scaling an object independently of its children, non-uniform scales (where the X, Y, and Z
scales are not all the same) are not fully supported, and may result in unexpected behavior. If you need to adjust the
scale of each axis independently, you should use the [Scale Adjuster](scale-adjuster.md) component in addition to Move
Independently.
