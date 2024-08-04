# Object Toggle

<!-- TODO: Screenshot -->

The Object Toggle component allows you to change the active state of one or more other GameObjects, based on the active
state of a controlling object.

## When should I use it?

This component is useful to disable one mesh when another mesh is covering it entirely. For example, you might want to
disable an underwear mesh when it's fully covered by other clothing.

## Setting up Object Toggle

Simply add an Object Toggle component to the controlling object, then click the + and select a target object to be
controlled. The checkmark controls whether the target object will be enabled or disabled.

### Conflict resolution

When multiple Object Toggles are active and try to control the same target object, the Object Toggle that is last in
hierarchy order will win. When no Object Toggles are active, the original state of the object, or animated state (if
some other animation is trying to animate that object) wins.

### Response timing

Object Toggle updates the affected objects one frame after the controlling object is updated. To avoid any unfortunate
"accidents", when an Object Toggle is disabled, the object that was disabled (either the Object Toggle itself or one of
its parents) in its parent hierarchy will be disabled one frame later than they would otherwise. This ensures that if
you use Object Toggle to hide a mesh when it's fully covered, the covering mesh will remain visible until the same frame
as when the inner mesh is enabled again.

When you use an Object Toggle to control another Object Toggle, this delay only applies to each Object Toggle
individually. That is, if you have A -> B -> C, and A is turned off, the timing will be as follows:

* Frame 1: Nothing happens (A's disable is delayed)
* Frame 2: A is disabled (B's disable is delayed)
* Frame 3: B and C are disabled at the same time.

### Preview system limitations

The effect of Object Toggles on mesh visibility is immediately reflected in the editor scene view. However, the impact
of Object Toggles on other responsive components, such as other Object Toggles or [Shape Changers](./shape-changer.md)
will not be reflected in the preview display. To see the full effect of Object Toggles, you must enter play mode.