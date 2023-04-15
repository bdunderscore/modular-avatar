# Control Group

The control group component allows multiple toggles to be grouped so that only one can be selected at a time.

![Control Group](control-group.png)

## When should I use it?

When you want to create an option that can be in one of several states - for example, an outfit switch.

## How do I use it?

Add a control group to a Game Object, and point your [Menu Item](menu-item.md) components at the Control Group.
The control group can be added to any game object, as long as it's inside your avatar, and does not contain another
MA Menu Item. Feel free to put it somewhere convenient for you.

You can configure the following options on a control group:
* Saved: If set, the current setting of the associated toggles will be preserved upon changing worlds or avatars
* Synced: If set, the current setting of the associated toggles will be synced and visible to other players
* Initial setting: The initial setting of this control group. If you choose "(none selected)", the default will be to
have no toggles selected. Otherwise, the specified toggle will be selected by default. Note that if you set a default
toggle, deselecting all toggles won't be possible.

The "Bound menu items" section allows you to see all the menu items linked to this control group.

Note that the control group is only used with menu items driving [action components](action-toggle-object.md); for
traditional toggles driving animator parameters, simply set those toggles to the same parameter name.

### Attaching Actions to control groups

You can attach actions such as [Action Toggle Object](action-toggle-object.md) to a control group. These actions will be
applied as defaults when selecting a menu item which does not specify what to do with a particular object. This can be
used to, for example, turn off the initial outfit in an outfit selector, so that you don't need to copy those toggles
to every alternate outfit.