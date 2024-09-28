# Visible Head Accessory

![Visible Head Accessory component](visible-head-accessory.png)

This component can be used to make a GameObject placed under the Head bone visible in first-person view.

## When should I use it?

When you want to see your own hair, or other accessories attached to your head, without needing to look in a mirror.

## When shouldn't I use it?

This component cannot be used as the child of a PhysBone chain (you can add it in the parent instead).

Using this component on _all_ children of the Head can be distracting, as your bangs continually get in the way.

Finally, the processing involved in this component is somewhat heavyweight, and may result in slower build times.

## Setting up Visible Head Accessory

Just attach a Visible Head Accessory component under a child of the Head bone. There are no configuration options to set.

The component will automatically generate a clone of the Head bone, which is connected to the real head bone using a parent constraint.
Only one constraint will be generated, even if multiple Visible Head Accessory components are used. As such, the performance impact of this component is the same whether you use one or dozens.
