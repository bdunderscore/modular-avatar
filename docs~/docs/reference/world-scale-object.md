# World Scale Object

This component can be used to force a game object to have the same scale as the world, regardless of the current avatar
scale. It will attach a (VRC) Scale Constraint to the game object, and set the constraint to scale to 1,1,1 scale relative
to the world.

## When should I use it?

When you want to have a game object scale with the world, rather than the avatar. This can be useful in certain complex
constraint gimmicks.

## Setting up World Scale Object

Simply attach the `World Scale Object` component to a GameObject. There are no configuration options to set.

Note that `World Scale Object` currently is not previewed in the Unity Editor, but will work correctly in-game or in
play mode.