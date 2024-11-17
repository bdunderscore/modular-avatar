# Parameters

The Modular Avatar Parameters component allows you to define the animator parameters your gimmick uses either internally,
or to communicate with other components. It renames parameters to avoid conflicts, and defines synced and unsynced
animator parameters and their defaults

![Parameters UI](parameters.png)


## When should I use it?

The Parameters component should be used when you're building a gimmick which makes use of animator parameters other
than VRChat builtins.

## How do I use it?

Each entry in the MA Parameters list configures a single parameter, or a prefix used for a VRChat PhysBone.
You set the name (or prefix) in the top row, with the type of parameter next to it.

### Parameter types

The parameter type field in the upper right can be set to any of the following:

* Bool
* Int
* Float
* Animator Only
* PB Prefix

If you select Animator Only, the parameter will not be added to the Expressions Parameters list. However, it will still
be able to rename the parameter in question, as described below.

The PB Prefix setting is used when this parameter prefix is set in a PhysBones component. As with Animator Only, this will
not be added to the Expressions Parameters list.

### Renaming parameters

If you enter a name in the "Change name to" field, the parameter will be renamed to that name for anything _outside_
of the MA Parameters object and its children. This can be useful for avoiding conflicts between different gimmicks,
or conversely, deliberately connecting two different gimmicks by making them use the same parameter.

You can also click the "Auto rename" box to have Modular Avatar automatically select an unused name for you.

### Default values

You can set a default value for each parameter. This value will be used when you avatar is reset. If you leave the
default box blank, then the value (if any) in the main Expressions Parameters asset will be used, or otherwise zero (or
false) will be used.

If you click the "Override Animator Defaults" box, then any default values specified in the _animator controller_ of
your asset will be changed to this default. This is occasionally useful with particularly complex gimmicks. If you
selected "Animator Only" and specified a default value, then this box will be ignored, and the animator controller
default will always be replaced.

### Saved/Synced

The Saved box controls whether the parameter will be saved across avatar changes and restarting VRChat.

The Synced box controls whether the parameter will be synced across the network. If you clear this box, this parameter
won't use your limited parameter space.

### Creating new parameters

You can define a new parameter in two different ways. First, you can click the "+" button at the bottom of the list of
parameters; then click the chevron next to the parameter to set its name.

Second, you can expand the "Unregistered Parameters" section; this section lists parameters which have been
detected in components inside this GameObject and its children. You can click the "Add" button to add the parameter,
or the magnifying glass to see where the parameter was detected.

Either way, after creating the parameter, click the chevron next to the new parameter to expand the detailed view.
There, you can set the parameter type (which controls whether the parameter is synced), and other attributes of the
parameter.

### Nesting

MA Parameters components can be nested. This lets you build up a complex system out of multiple subcomponents. Each
MA Parameters component can apply renamings to all of its children. This means that if you have an inner MA Parameters
which renames "foo" to "bar", and an outer MA Parameters which sets "bar" to "auto rename", you can still access "bar"
in the objects in-between.

There are a few notable subtleties when nesting components:

* The "Saved" parameter will take the outermost "Saved" setting. However, when multiple MA Parameters components which
  are not nested set "Saved" to different values, the parameter will be saved if any of the components set it to be
  saved.
* The "Default Value" field will take the outermost setting; however, if outer components have a blank default value,
  the innermost non-blank default value will be used. If multiple components which are not nested set a non-blank
  default value, a warning will be shown, as it's unclear which should be used.