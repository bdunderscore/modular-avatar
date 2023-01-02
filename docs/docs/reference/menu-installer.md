# Menu Installer

The Modular Avatar Menu Installer allows you to easily add menu items to the avatar's expressions menu.

![Menu Installer](menu-installer.png)

## When should I use it?

When you have a menu item to add!

## How do I use it?

### End-users

By default, the prefab's menu will be installed at the top level of your avatar's action menu.
If that's what you want, you're done! Otherwise, click "Select Menu" and double-click the menu you want to install the prefab's controls to.

If the selected menu gets full, it will be automatically split into multiple pages (submenus).

If you want to disable the menu installation entirely, click the disable checkbox in the upper-left of the menu installer inspector.

### Prefab developers

First, create an expressions menu with the controls you want to add. This menu will be _appended_ to a selected submenu of the avatar's Expressions Menu tree.
As such, if you want a submenu of your own, you will need to create two menu assets: One for the submenu control, and one for the inner menu itself.

Add a Menu Installer component to your prefab, at the same level as your [Parameters](parameters.md) component.
Then, open the Prefab Developer Options tag, and add the desired menu to "Menu to install". Done!

### Extending menus of other assets

In some cases, it can be useful to extend a menu that is being installed by another Menu Installer component.
This can be done by specifying the menu asset (or a submenu) that is being installed by a Menu Installer in the "Install To" field.