# Merge Armature

The Merge Armature component merges a tree of GameObjects onto the armature of the avatar.

![Merge Armature](merge-armature.png)

## When should I use it?

The Merge Armature component is designed specifically for clothing prefabs, and includes special logic for updating existing Skinned Mesh Renderer references and minimizing the number of generated bones. You should use it if you intend to create a skinned mesh which tracks the avatar's armature.

## When shouldn't I use it?

The Merge Armature component is not designed for use in prefabs that are intended to be able to apply generically to many different avatars. It would not be a good fit for a finger-pen prefab, for example.

Because the Merge Armature component assumes that the bones you are binding to do not move, it is not able to generalize to avatars other than the one it was set up with.

## Setting up Merge Armature

After adding the Merge Armature component to the root of the source hierarchy, drag the avatar's corresponding bone (where these should be merged to) onto Merge Target. Prefix and Suffix will normally be set automatically.

## How does it work?

Merge Armature will walk the tree of GameObjects under the GameObject it is attached to, and search for the corresponding bone in the base avatar by name. For better compatibility with existing avatars, you can optionally specify a prefix and/or suffix which will be removed from the merged bones when looking for a match.
If a match is found, Merge Armature will attempt to rewrite references to the merged bone to instead point to the base avatar's corresponding bone. In some cases this is not possible, and a child bone will be created instead.
If a match is not found, a child bone will be created under the corresponding parent bone.

Merge Armature goes to a lot of trouble to ensure that components configured on the source hierarchy, or pointing to it, Just Work (tm). In particular, it will:
* Update animator references to point to the appropriate position, depending on what properties are being animated (e.g. transform animations will point to the post-merge bone, GameObject active animations will point to the source heirarchy)
* PhysBones and contacts will have their target field updated to point to the new merged bones. This will happen even if the PhysBone is not located under the Merge Armature component.
* Other components will remain on the source hierarchy, but constraints will be generated to track the merged hierarchy.

Merge Armature will leave portions of the original hierarchy behind - specifically, if they contain any components other than Transforms, they will be retained, and otherwise will generally be deleted.
Where necessary, PhysBone objects will have their targets updated, and ParentConstraints may be created as necessary to make things Just Work (tm).

## Locked mode

If the locked option is enabled, the position and rotation of the merged bones will be locked to their parents in the editor. This is a two-way relationship; if you move the merged bone, the avatar's bone will move, and vice versa.

This is intended for use when animating non-humanoid bones. For example, you could use this to build an animator which can animate cat-ear movements.

## Object references

Although the editor UI allows you to drag in a target object for the merge armature component, internally this is saved as a path reference.
This allows the merge armature component to automatically restore its Merge Target after it is saved in a prefab.