# Mesh Settings

![Mesh Settings](mesh-settings.png)

The Mesh Settings component lets you set certain mesh settings (anchor override and bounds) for all meshes
under a particular game object.

## When should I use it?

You can place this component at the top level of your avatar to ensure that bounds and light probe anchors
are consistent for all meshes in your avatar.

The "Setup Outfit" feature will also automatically configure a Mesh Settings component on newly added outfits.

Finally, Mesh Settings can be used to _exclude_ meshes from the influence of Mesh Settings higher up on the
hierarchy.

## When shouldn't I use it?

Setting bounds or light probes on assets for distribution requires some care, as these configurations
might be inconsistent with the avatar they are applied to. Generally, these should only be set on assets
designed for a specific avatar.

## Manually configuring Mesh Settings

When you add Mesh Settings to a game object, initially it is configured to do nothing. By setting either
"Anchor Override Mode" or "Bounds Override Mode" to "Set", you can configure the anchor override or bounds
for all meshes under that game object. Alternately, by setting the mode to "Don't set", you can exclude
these meshes from the influence of Mesh Settings higher up on the hierarchy.

When configuring bounds, the bounding box will be determined relative to the transform you specify as the
"Root Bone". Note that bounds only affects Skinned Mesh Renderers, but Anchor Override also impacts other
types of renderers like Mesh Renderers or Line Renderers.
