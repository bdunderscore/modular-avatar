# Platform Filter

The Platform Filter component allows you to include or exclude specific GameObjects from your avatar based on the target VRSNS platform (such as VRChat, Resonite, etc).

## When should I use it?

Use Platform Filter when you want certain objects or components to only be present on specific platforms. For example,
you may want to include a VRChat-only gimmick only on VRChat.

## When shouldn't I use it?

Many Modular Avatar features already handle platform-specific restrictions. For example, Merge Animator already only
operates on VRChat. As such, adding a Platform Filter isn't always necessary.

## Manually configuring Platform Filter

Add the Platform Filter component to any GameObject you wish to filter. You can add multiple Platform Filter components
to the same GameObject to specify multiple platforms. Each filter can be set to either **Include** or **Exclude** a platform:

- **Include**: The GameObject will only be present when building for one of the specified platform(s).
- **Exclude**: The GameObject will be removed on the specified platform(s).

If a GameObject has both include and exclude filters, an error will be reported.

## Example Usage

- To make an object appear only on VRChat, add a Platform Filter, set Platform to "VRChat", and set to **Include**.
- To hide an object on Resonite, add a Platform Filter, set Platform to "Resonite", and set to **Exclude**.