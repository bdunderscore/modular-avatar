# Portable Avatar Components

NDMF provides a number of components that can be used to create an avatar that does not depend on a specific platform SDK (e.g. the VRCSDK) for building.

A minimal setup for a portable avatar consists of three components:
- **NDMFAvatarRoot**: This component is required for all avatars. Place it at the root of your avatar.
- **NDMF Viewpoint**: This component sets the viewpoint of your avatar. Create an empty gameobject at the position of your viewpoint, and add this component to it. The viewpoint object can be anywhere in the hierarchy, but it is recommended to place it at the root of your avatar.
- **NDMF Blendshape Visemes**: This component is required for avatars that use blendshape visemes for lipsync. Use it to configure the face mesh and the viseme blendshapes.

## Portable Dynamic Bones {#portable-dynamic-bones}

Portable dynamic bone components let you mark a bone as being physically simulated without depending
on any specific SDK. Because each platform has its own way of simulating physics, these components
only configure the minimal set of properties that are common to all platforms. Specific configuration
is handled by configuring a "settings template" name; you can configure the settings template
in a platform-specific way to give it specific settings.

If you have both a portable dynamic bone component and a platform-specific dynamic bone component
referring to the same bone root transform, the platform-specific component will take precedence.

:::warning

Portable dynamic bones are not fully functional yet. They might work,
but are highly likely to change in incompatible ways in the future.

:::

<!-- TODO: Details -->