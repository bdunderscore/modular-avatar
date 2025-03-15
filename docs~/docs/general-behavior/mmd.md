# MMD World Workarounds

Some "MMD Worlds" in VRChat have a behavior where they will disable the _second and third_ layers of your FX animator
controller. This is intended to disable the layers controlling your facial expressions, so the MMD world can override
them.

Modular Avatar will automatically arrange for whichever layers were _originally_ layers 2 and 3 to be disabled in this
circumstance. That is, if a layer is added before them, MA will add some relay layers to drive layers 2 and 3 off and
on appropriately.

Layers added via Merge Animator (even in replace mode) will not be affected by this MMD world behavior; if necessary,
padding layers will be added to protect them. If you want to opt them into this behavior, you can attach the `MA MMD
Layer Control` _state machine behavior_ to the layer you want to control.

:::warning

The `MA MMD Layer Control` state machine behavior will only work when attached to the layer directly. Due to how state
machine behaviors work, I can't stop you from attaching them to individual states - but this will break your build
(so don't do that).

:::

:::note

This workaround only works for worlds which specifically disable layers 2 & 3. Given current VRChat constraints, it's
not possible to provide a more general solution.

:::