# Sync Parameter Sequence

![Sync Parameter Sequence](sync-parameter-sequence.png)

On VRChat, it's necessary for parameters that are shared between different-platforms of an avatar (e.g. PC and Android)
to appear at the start of the expressions parameters list, and in the same order. This component adjusts the order of
your expressions parameters, and adds additional parameters where necessary, to ensure that your avatar syncs properly
between PC and Android.

## When should I use it?

You should use this component if you are uploading different versions of the same avatar to PC and Android, and both
versions make use of synced expressions parameters.

## When shouldn't I use it?

This component may have compatibility issues with certain VRCFury components, such as Parameter Compressor.

## How should I use it?

First, attach the Sync Parameter Sequence component to any object on your avatar. Then, click the New button to create
an asset to save the parameter sequence. On other platform variants of your avatar, attach the component, and select the
asset you just created. Upload on Android (or whichever　platform you want to be the primary platform), then upload for
other platforms as well.

Whenever you upload your avatar on the platform listed as "Primary Platform", Modular Avatar will record its expression
parameters in this asset. Then, later, when you upload on some other platform, Modular Avatar will adjust the order of
the parameters to match the primary platform.

## Parameter limits

The Sync Parameter Sequence component will add additional parameters to your avatar if necessary to ensure that the
order of parameters matches between platforms. This may cause your avatar to exceed the maximum number of parameters,
in which case the build will fail.

To address this, you can clear the contents of the parameters asset to clear out obsolete parameters; otherwise, make
sure you don't have a lot of both android-only and PC-only parameters, because you'll end up using the combination of
both.