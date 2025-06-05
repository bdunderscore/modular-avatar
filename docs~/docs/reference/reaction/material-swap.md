# Material Swap

![Material Swap](material-swap.png)

The Material Swap component allows you to swap materials on your avatar with other materials in bulk,
when the Material Swap component's GameObject is enabled.

Material Swap is a type of [Reactive Component](./index.md). See that page for general rules and behavior of reactive
components.

## When should I use it?

Material Swap can be used to swap materials on your avatar in bulk, either directly in response to a menu item, or in
response to some other object appearing or disappearing.

In Material Setter, you specify the renderers whose materials will be changed, whereas in Material Swap, you specify the materials to be swapped.

## Setting up Material Swap

Attach the Material Swap component to the GameObject that will control its state. This can either be an object that
will be animated to enable/disable it, or it can be on a Menu Item (or a child thereof). You can also attach it to an
object that is always enabled, to swap materials on your avatar at all times.

Next, click the + button to add a new entry.
Drag the material you want to swap from onto the upper material field,
and drag the material you want to swap to onto the lower material field.

By default, Material Swap will swap materials when the GameObject is enabled (and/or the associated menu item is
selected). If you want to swap materials when the GameObject is disabled, you can select "Inverse condition".