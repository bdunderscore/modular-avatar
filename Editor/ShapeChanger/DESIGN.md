# Semantics

Shape Changer will set the specified shape keys when the GameObject it is on is active and enabled.
This analysis is based on the original hierarchy position of the GameObject in question, not the position after any
Merge Armature or Bone Proxy operations.

## Shape Key Modes

Shape keys can be set to either

* Set - Set the shape key to the given value
* Delete - If the GameObject is always active, deletes the associated triangles and vertices. Otherwise, sets the shape
  key to 100%.

## Conflict resolution

First, if any Delete action is active, we will delete the affected vertices (if static) or set to 100 (if dynamic).
Otherwise, the last object in hierarchy traversal order wins.

Implementation-wise, we first determine if the result is static (ie, if the last object - or multiple objects with
consistent settings - is non-animated). If so, we take that result.

Otherwise, we:

* Create animator parameters to proxy game object active states. These are injected into any animation clips which
  manipulate relevant game objects.
* Build a blend tree to determine the final state.
  * For delete actions, we create a blend tree which effectively adds together all the active objects.
  * For set actions, we create a blend tree which selects the last active object.
    * This blend tree works by exploiting the FP32 exponent field. Each object is assigned a floating point value with a
      unique exponent and a zero mantissa; we skip every other exponent here, in order to provide a "guard band" between
      values. We then sum these using a direct blend tree.
  * We finally use a two-level 1D blend tree to evaluate the delete action and then any set actions.
