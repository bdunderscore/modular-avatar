# World Fixed Object

![World Fixed Object component](world-fixed-object.png)

This component can be used to make a GameObject fixed to world.

## When should I use it?

When you want to make some object world-fixed.

## When shouldn't I use it?

## Setting up World Fixed Object

Attach a World Fixed Object your object to a GameObject fixed to world. There are no configuration options to set.

The component will automatically generate world-origin fixed GameObject at the avatar root and move your GameObject to their child.
Position of GameObjects with a World Fixed Object can be adjusted using Parent Constraint, etc.

Only one constraint will be generated, even if multiple World Fixed Object components are used.
As such, the performance impact of this component is the same whether you use one or dozens.

Due to technical limitations on the Quest, this component has no effect when building for Quest standalone.
