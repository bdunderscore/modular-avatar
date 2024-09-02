# Convert Constraints

The Convert Constraints component directs Modular Avatar to nondestructively convert Unity constraints to VRChat
constraints on build. It will convert any constraints (and animations referencing them) on the same object it is
attached to, and any children of that object. It will also attempt to fix animations broken by using VRCSDK's Auto Fix
button with older versions of Modular Avatar.

## When should I use this?

It's probably a good idea to put this on your avatar root in most cases, as preconverting constraints improves
performance significantly. When MA is installed, the VRChat Auto Fix button will automatically add this component to
your avatar root if it's not already there.

## When should I not use this?

This component is primarily provided to allow users to disable this functionality (by removing this component) if it is
suspected to be causing problems.