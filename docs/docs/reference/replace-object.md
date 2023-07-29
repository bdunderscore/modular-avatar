# Replace Object

The Replace Object component allows you to completely replace the contents of a GameObject on the parent avatar.

## When should I use it?

The Replace Object component is useful when you want to replace an object on the parent avatar with a different object.
For example, you might want to replace the PhysBones configuration of the base avatar, or replace its body mesh entirely
with a different mesh.

## When should I not use it?

An object can only be replaced by one other object. As such, when you use Replace Object, you will limit the
compatibility of your asset with other assets that might also want to use Replace Object.

## Detailed operation

### Handling of child objects

Replace Object only replaces the specific object that was specified. The child objects of both the original and the
replacement object will both be placed under the replacement object.

### Object naming

Replace Object does not change the name of your replacement object; if its name is different from the original object,
then the final object name will be different. However, Replace Object _does_ update any animation paths that reference
the original object to reference the replacement object instead.

Because Replace Object is performed fairly late in avatar processing, in most cases this does not make much of a 
difference. However it may matter if - for example - you are replacing the `Body` mesh and want to maintain MMD world
compatibility (or, conversely, want to add MMD compatibility to an existing avatar).

### Handling of component references

Replace Object will attempt to fix any references to components on the old object to point to the new object instead.
If there is more than one of the same component on the old object, then references will be matched against the component
with the same index in the new object (or nulled out if the new object doesn't have enough of that component type).

Replace Object will not perform fuzzy matching; if, for example, you replaced a Box Collider with a Sphere Collider,
references to the old Box Collider will become null.