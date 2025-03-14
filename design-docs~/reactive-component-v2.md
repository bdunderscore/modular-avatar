# Goals

* Single frame evaluation
* Parameter drivers
* Condition overrides
* Extensibility
  * We need to support NDMF extensions adding new conditions, new reactions, and changing how existing reactions are
    animated
  * XDress as a sample case

## General concept: Object conditions

By default, the condition of an object is a boolean value determined by ANDing the object's active state, any condition
components on the object, and the condition of its parent.

The Condition Override component can be used to override this. If this component is present, the condition of the object
is replaced by the condition specified by the Condition Override component. This condition can specify an OR of (not)
ANDs, each of which can contain:

* A condition component
* A game object's active state
* A game object's _condition_ (including its parents)

## Evaluation

We need to convert these conditions into blend trees in the end. Generally, 1D or 2D freeform blend trees are used as
boolean primitives. Complex conditions may require multiple frames to evaluate, as a direct conversion would result
in a O(2^n) blend tree nodes; as such, we instead consider that an OR should be evaluated by adding up each of its
branches in an internal parameter, and in a subsequent frame operating based on whether that parameter is >= 1.

## Wavefronts

To avoid objects triggering at different times, we synchronize the state change of all related objects. A group of such
objects is called a _wave_, and a single evaluation is a _wavefront_. To construct a wave, we build an undirected graph
consisting of connections between gameobject _active state_ inputs, virtual nodes corresponding to condition components
and object conditions, and gameobjects animated by reactions. Then, each subgraph of this graph is a wave.

Within a wave, we arrange for all objects to respond with the same latency in frames. This is accomplished by adding
buffering stages to the decision blendtrees where needed. For gameobject _active state_ inputs, we convert them into
parameter driving curves, and then apply those curves to the object's active state after an appropriate delay.

Note: For AAO compatibility, we need a prepass to merge multiple objects with identical animations into a single parameter
and driving animation.

# Extension APIs

## What do we want to do?

### XDress

XDress needs to operate on an entire wave at a time. It will in particular need to:

* Add additional delay to reactions outside of its scope (?)
* Virtualize some reactions into parameters so it can apply its own animations afterward

### Custom conditions

We need to generate a new condition in the form of a blendtree (or multiple blendtrees). The framework will provide a
"true" or "false" motion that the blendtree will branch to.

### Custom reactions

Apart from the XDress usecase where we virtualize as parameters, we also want to be able to directly drive serialized
properties.

### Parameter drivers

Parameter drivers require a separate layer, due to the need to use state behaviors

TODO: substatemachine tricks
TODO: loop handling