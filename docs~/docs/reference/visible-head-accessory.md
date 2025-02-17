# Visible Head Accessory

![Visible Head Accessory component](visible-head-accessory.png)

This component can be used to make a GameObject placed under the Head bone visible in first-person view.

## When should I use it?

When you want to see your own hair, or other accessories attached to your head, without needing to look in a mirror.

## When shouldn't I use it?

This component cannot be used as the child of a PhysBone chain (you can add it in the parent instead).

Using this component on _all_ children of the Head can be distracting, as your bangs continually get in the way.

## Setting up Visible Head Accessory

Just attach a Visible Head Accessory component under a child of the Head bone. There are no configuration options to set.

## How it works

On VRChat, the component uses VRCHeadChop to make the selected bones visible. The main difference between this and
simply using VRCHeadChop, is that it adjusts the mesh to ensure that triangles don't clip through the player viewpoint.

This is done by looking for triangles that have some vertices weighted to a visible bone, and some weighted to a hidden
bone, such as the root `Head` bone. The component then adjusts the mesh, adding new proxy bones and switching the weights
over to ensure that the triangle is fully visible.