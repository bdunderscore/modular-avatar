# Menu Item

The Menu Item component allows you to define an expressions menu item from within the Unity hierarchy.

![Menu Item](menu-item.png)

## When should I use it?

This component can provide a more convenient way to edit and define menu items than defining VRC Expressions Menu assets. You can move menu items around by dragging and dropping them through the hierarchy, and it provides an editor interface that is aware of parameter names defined on [MA Parameters](parameters) components. In combination with [action components](action-toggle-object), you can even define object toggles without having to create any animations or animators.

## How do I use it?

:::tip

A full tutorial on using the menu editor system is available [here](../tutorials/menu).

:::

The menu item component defines a single menu item in a larger menu. You can configure the icon, menu type, [control group](control-group), and parameter for the menu item. The name of the menu item will be taken from the name of the containing game object. This lets you see the name of, and rename menu items directly from the hierarchy.

### Submenus

When menu items are set as a submenu, you can configure where the submenu is sourced from. You can either set submenu source to "Expressions Menu Asset" and configure a traditional VRC Expressions Menu asset to reference, or you can set the 'submenu source' to Children, in which case Menu Items attached to direct children of this menu item are used to populate the submenu.

If the number of items in the submenu exceeds the maximum number of items on a VRC menu, a "next" item will automatically be created to split up the menu.

When submenu source is children, you can also specify a "source object override". If set, the children of that object will be used, instead of the direct children of the menu item.

### Binding submenus

In order to define where a menu item will go in the menu, another component will be needed to _bind_ it to a menu. There are three ways of doing this:

* The menu item can be set as the child of another menu item set in submenu / children mode.
* The menu item can be on the same game object as a [Menu Installer](menu-installer) component.
* The menu item can be the child of a [Menu Group](menu-group) object (which would typically be on a game object with a Menu Installer component)

Unbound menu items have no effect

### Using with actions

If an [action component](action-toggle-object) is on the same object as the menu item, the menu item will be configured to control this action component, instead of controlling an arbitrary parameter. See the action component documentation for details.

When an action component is present on the same object, you can no longer select the parameter name for the menu item; Modular Avatar will automatically assign a parameter at build time. By default, a boolean parameter will be created; if you attach a [control group](control-group) to the menu item, an int parameter will be used instead.

When a control group is attached, you can select a single menu item to be the "Group Default". When this is done, this menu item will be set to be initially selected; additionally, for certain types of actions (notably, [Toggle Object](action-toggle-object) actions), when _other_ menu items are selected, the action of this default item will be negated (so any objects this action toggles on, will be off by default in other states). 