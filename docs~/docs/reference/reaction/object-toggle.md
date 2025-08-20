# Object Toggle

![Object Toggle](object-toggle.png)

The Object Toggle component allows you to change the active state of one or more other GameObjects, based on the active
state of a controlling object.

Object Toggle is a type of [Reactive Component](./index.md). See that page for general rules and behavior of reactive
components.

## When should I use it?

This component is useful to disable one mesh when another mesh is covering it entirely. For example, you might want to
disable an underwear mesh when it's fully covered by other clothing.

## Setting up Object Toggle

Simply add an Object Toggle component to the controlling object, then click the + and select a target object to be
controlled. The checkmark controls whether the target object will be enabled or disabled.

### Conflict Resolution

When multiple Object Toggle components are active and attempt to control the same target object, the Object Toggle
that appears last in hierarchy order will take precedence.

If all Object Toggle components controlling a target object are inactive, the object's original state, or (if other
animations are manipulating that object) animated state will be used.

### Response Timing

Object Toggle updates affected objects one frame after the controlling object is updated. To avoid unfortunate
"accidents", when an Object Toggle is disabled, the disabled object (the Object Toggle itself or its parent, etc.)
is disabled one frame later than usual. This ensures that when disabling outer clothing, the outer mesh continues
to be displayed until the inner mesh is re-enabled.

When using Object Toggle to control other Object Toggles, this delay applies only to each Object Toggle. That is,
if A -> B -> C and A is turned off, the timing will be as follows:

* Frame 1: Nothing happens (A's disable is delayed)
* Frame 2: A is disabled (B's disable is delayed)  
* Frame 3: B and C are disabled simultaneously.
