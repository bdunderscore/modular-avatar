# Virtual Menu Subsystem Design Documentation

## Overview

The Virtual Menu subsystem is a core component of Modular Avatar that enables flexible, composable menu construction for VRChat avatars. It provides a non-destructive way to combine menu items from multiple sources (base avatar menus, menu installers, menu items, and menu groups) into a unified menu structure before serializing to VRChat's `VRCExpressionsMenu` assets.

## Goals and Requirements

### Primary Goals

1. **Non-destructive composition**: Enable menu items from multiple prefabs and components to be combined without modifying original assets
2. **Cycle handling**: Support menu structures with cycles (e.g., a menu that links back to a parent menu)
3. **Deferred resolution**: Allow menu nodes to reference each other before their contents are fully determined
4. **Parameter remapping**: Support parameter name transformations through postprocessor hooks
5. **Install targeting**: Enable menu items to be installed into specific submenus of the avatar's menu tree
6. **Overflow handling**: Automatically split menus that exceed VRChat's control limit (8 controls per menu)

### Non-Goals

- Real-time menu manipulation during play mode
- Optimization of menu structure (e.g., removing duplicates)
- Validation of menu content beyond basic structure

## Architecture

The virtual menu subsystem consists of three main layers:

### 1. Runtime API Layer (`nadena.dev.modular_avatar.core.menu`)

Defines the core abstractions and data structures that other components can reference. This layer is intentionally simple and stable, though marked as unstable due to C# visibility requirements.

Key types:
- `VirtualMenuNode`: Represents a single menu before overflow splitting
- `VirtualControl`: A menu control that can reference virtual nodes instead of concrete menu assets
- `MenuSource`: Interface for objects that can contribute controls to a menu
- `NodeContext`: Callback interface for building menu node contents

### 2. Editor Implementation Layer (`nadena.dev.modular_avatar.core.editor.menu`)

Contains the actual implementation of virtual menu construction and serialization. This is where the complex graph traversal and resolution logic lives.

Key types:
- `VirtualMenu`: The main orchestrator that builds the complete menu graph
- `NodeContextImpl`: Implementation of the visitor pattern for menu construction
- `RootMenu`: Sentinel object representing the avatar's root menu

### 3. Build Integration Layer

Integrates the virtual menu system into the NDMF build pipeline and provides menu installation functionality.

Key types:
- `MenuInstallHook`: NDMF build pass that constructs and serializes virtual menus
- `FixupExpressionsMenuPass`: Post-processing for menu assets (icon scaling, parameter validation)

## Core Data Structures

### VirtualMenuNode

```csharp
public class VirtualMenuNode
{
    public List<VirtualControl> Controls = new List<VirtualControl>();
    public readonly object NodeKey;
}
```

A `VirtualMenuNode` represents a single menu before serialization and overflow splitting. Key characteristics:

- **NodeKey**: Identifies the source of this menu node. Can be:
  - A `VRCExpressionsMenu` asset
  - A `MenuSource` component (e.g., `ModularAvatarMenuItem`, `ModularAvatarMenuGroup`)
  - `RootMenu.Instance` for avatars without a root menu
  - A tuple `(menu, postprocessor)` when parameter remapping is involved

- **Controls**: The list of controls in this menu. These are added during the node visitation phase.

### VirtualControl

```csharp
public class VirtualControl : VRCExpressionsMenu.Control
{
    public VirtualMenuNode SubmenuNode;
}
```

A `VirtualControl` extends VRChat's standard control structure with a reference to a `VirtualMenuNode` instead of a concrete `VRCExpressionsMenu`. This enables:

1. Cycles in the menu graph (a submenu can reference a parent)
2. Deferred resolution (submenus don't need to exist when the control is created)
3. Parameter postprocessing (different paths to the same menu with different parameter mappings)

### MenuSource Interface

```csharp
public interface MenuSource
{
    void Visit(NodeContext context);
}
```

The `MenuSource` interface allows components to contribute menu items. Implementations include:

- `ModularAvatarMenuItem`: Generates a single menu control
- `ModularAvatarMenuGroup`: Includes all menu items from child objects
- `ModularAvatarMenuInstaller`: Includes controls from a VRCExpressionsMenu asset
- `MenuNodesUnder`: Helper that includes all `MenuSource` children of a GameObject

### NodeContext Interface

```csharp
public interface NodeContext
{
    void PushMenuContents(VRCExpressionsMenu expMenu);
    void PushNode(MenuSource source);
    void PushNode(ModularAvatarMenuInstaller installer);
    void PushControl(VRCExpressionsMenu.Control control);
    void PushControl(VirtualControl control);
    VirtualMenuNode NodeFor(VRCExpressionsMenu menu);
    VirtualMenuNode NodeFor(MenuSource menu);
}
```

The `NodeContext` provides callbacks for menu sources to add controls and reference other menus. It handles:

- Cycle detection (via a visited set)
- Deferred node creation (nodes are created on-demand)
- Parameter postprocessing (applies transformations to control parameters)
- Menu installer invocation (when a VRCExpressionsMenu is extended by installers)

## Menu Construction Process

The virtual menu is constructed in several phases:

### Phase 1: Registration

During this phase, components register their contributions without actually building the menu structure:

1. **Scan for installers**: Find all `ModularAvatarMenuInstaller` components on the avatar
2. **Build installer map**: Group installers by their target menu (either a specific menu asset or the root menu)
3. **Scan for install targets**: Find all `ModularAvatarMenuInstallTarget` components
4. **Build target map**: Track which installers are referenced by install targets

This produces two maps:
- `_targetMenuToInstaller`: Maps menu keys to the list of installers that target them
- `_installerToTargetComponent`: Maps installers to the install target components that reference them

**Important**: Installers that are referenced by any install target (even disabled ones) are excluded from the first map. This prevents them from being automatically installed at their configured location while still allowing them to be explicitly referenced.

### Phase 2: Freezing

The `FreezeMenu()` method initiates the graph traversal:

1. Create the root `VirtualMenuNode` with key `RootMenuKey`
2. Create a `NodeContextImpl` for the root
3. If the avatar has a root menu asset, push its controls
4. Push any installers that target the root menu asset or root key
5. Process the pending generation queue until empty

The pending generation queue is populated by `NodeFor()` calls, which create new nodes on-demand. This allows controls to reference submenus that haven't been fully resolved yet.

### Phase 3: Node Visitation

When a node needs to be populated, its key determines how it's processed:

#### For VRCExpressionsMenu Assets

1. Push all controls from the menu asset
2. Look up any installers that target this menu
3. For each installer, invoke `PushNode(installer)`
4. Mark the menu as visited (in the visited set)

#### For MenuSource Components

1. Invoke the `Visit()` method on the source
2. The source pushes controls and/or other nodes through the context

#### For ModularAvatarMenuInstaller

1. If the installer has a `MenuSource` component, visit it
2. Otherwise, push the contents of `menuToAppend`
3. Apply any parameter postprocessing configured for this installer

### Phase 4: Control Processing

When a control is pushed through `PushControl()`:

1. Create a `VirtualControl` from the VRChat control
2. If the control has a submenu, call `NodeFor()` to get/create the virtual node
3. Apply the current postprocessor to the control (for parameter remapping)
4. Add the control to the current node's control list

### Phase 5: Serialization

The `SerializeMenu()` method converts the virtual menu graph into VRChat menu assets:

1. Start from the root menu key
2. For each node:
   - Create a new `VRCExpressionsMenu` ScriptableObject
   - Convert each `VirtualControl` to a `VRCExpressionsMenu.Control`
   - Recursively serialize referenced submenu nodes
   - Save the asset through the build context
3. Return the serialized root menu

The serialization process handles deduplication: if the same node key is encountered multiple times, the same menu asset is returned.

### Phase 6: Overflow Splitting

After serialization, `MenuInstallHook.SplitMenu()` handles menus that exceed the 8-control limit:

1. If a menu has ≤ 8 controls, do nothing
2. Otherwise:
   - Create a new "More" submenu
   - Move controls 8+ to the new submenu
   - Add a "More" control linking to the new submenu
   - Recursively process the new submenu

This creates a linked list of "More" pages when needed.

## Parameter Postprocessing

The `PostProcessControls` dictionary in `BuildContext` enables parameter name transformations. This is used by:

1. **RenameParametersHook**: Renames parameters based on `ModularAvatarParameters` configuration
2. **Custom extensions**: NDMF extensions can add their own transformations

### How It Works

1. When an installer or menu source is registered in `PostProcessControls`, its postprocessor function is stored
2. During node visitation, the postprocessor is activated via a `PostprocessorContext`
3. When controls are pushed, the active postprocessor modifies the control's parameter names
4. Different paths through the menu graph can have different active postprocessors

### NodeKey Disambiguation

When a `VRCExpressionsMenu` asset is visited with different postprocessors active, it creates different node keys:

- Without postprocessing: `menu`
- With postprocessing: `(menu, postprocessor)`

This ensures that the same menu asset can be included multiple times with different parameter mappings.

## Extension Points

### Adding Menu Sources

To add a new type of menu contribution:

1. Implement the `MenuSource` interface
2. For components, extend `MenuSourceComponent`
3. Implement `Visit(NodeContext context)` to push controls and/or nodes

Example:
```csharp
public class MyMenuSource : MenuSourceComponent
{
    public override void Visit(NodeContext context)
    {
        var control = new VirtualControl(/* ... */);
        context.PushControl(control);
    }
}
```

### Adding Parameter Transformations

To transform parameter names in menus:

1. Add a postprocessor to `BuildContext.PostProcessControls`
2. The key is the `MenuInstaller` or menu asset object
3. The value is `Action<VRCExpressionsMenu.Control>` that modifies the control

Example:
```csharp
context.PostProcessControls[installer] = control =>
{
    if (control.parameter != null)
    {
        control.parameter.name = TransformName(control.parameter.name);
    }
};
```

### Custom Menu Processing

NDMF extensions can:

1. Register installers programmatically via `VirtualMenu.RegisterMenuInstaller()`
2. Register install targets via `VirtualMenu.RegisterMenuInstallTarget()`
3. Add postprocessors to `BuildContext.PostProcessControls`
4. Implement custom `MenuSource` components

## Cycle Handling

The virtual menu system explicitly supports cycles in the menu graph:

1. **During visitation**: The `_visited` set in `NodeContextImpl` prevents infinite recursion
2. **During serialization**: Nodes are serialized on-demand, and a `serializedMenus` map prevents duplicate serialization
3. **In VRChat**: Cycles are perfectly valid (e.g., a "Back" control that returns to a parent menu)

Important: Cycles are temporarily broken during visitation (the second visit to a node is skipped), but the structure is preserved during serialization.

## Install Targets

`ModularAvatarMenuInstallTarget` provides explicit inclusion of one installer's content into another location:

1. The target component references a `ModularAvatarMenuInstaller`
2. When the target is visited, it invokes `PushNode()` on the referenced installer
3. Referenced installers are excluded from automatic installation at their configured location

This enables:
- Conditional menu inclusion (by disabling the install target)
- Reorganizing menu structure without modifying the original installer
- Breaking installer loops at a well-defined point

## Caching and Invalidation

The virtual menu system maintains a cache sequence number (`_cacheSeq`) that is incremented whenever menus are invalidated:

1. `RuntimeUtil.OnMenuInvalidate` events trigger `InvalidateCaches()`
2. Each `VirtualMenu` records the cache sequence at creation
3. `IsOutdated` property checks if the menu was created before the last invalidation

This enables UI code to detect when a cached virtual menu needs to be rebuilt.

## Error Handling and Validation

The virtual menu system uses `BuildReport.ReportingObject()` to attribute errors to specific components:

1. When visiting a node, errors are attributed to the source object
2. When processing an installer, errors are attributed to the installer component
3. This provides clear feedback in the NDMF error reporting UI

## Performance Considerations

### Graph Traversal

- Nodes are created lazily (only when referenced)
- The pending generation queue avoids deep recursion
- The visited set prevents redundant processing

### Memory Usage

- Virtual menus exist only during the build process
- After serialization, the virtual structure is discarded
- Menu assets are created on-demand and saved immediately

### Build Time

- Menu construction is O(n) where n is the total number of controls across all menus and installers
- Serialization is O(m) where m is the number of unique menu nodes
- Overflow splitting is O(k) where k is the number of controls in oversized menus

## Known Limitations

1. **Parameter context deduplication**: When the same submenu is used with different parameter remappings, the same node is returned regardless of context. This is noted as a FIXME in the code.

2. **Installer ordering**: When multiple installers target the same menu, they are processed in the order they were registered (scene hierarchy order). There is no explicit priority system.

3. **No optimization**: The system does not attempt to merge duplicate controls or optimize the menu structure.

4. **Single-threaded**: Menu construction is not parallelized, though it's generally fast enough that this isn't a concern.

## Future Enhancements

Potential improvements to the virtual menu system:

1. **Parameter context tracking**: Fix the FIXME mentioned in limitations by tracking the parameter postprocessor context when creating node keys for all menu types.

2. **Installer priorities**: Add an explicit priority system for installers targeting the same menu.

3. **Menu optimization**: Optional passes to deduplicate controls or reorganize for better UX.

4. **Validation hooks**: Extension points for custom menu validation rules.

5. **Preview API**: A stable API for editor UI to preview the virtual menu structure without triggering a full build.

## Related Systems

### NDMF Integration

The virtual menu system integrates with NDMF via:
- `MenuInstallHook`: Runs during the build pipeline to construct and serialize menus
- `BuildContext`: Provides the postprocessor dictionary and asset saving functionality

### Parameter System

The virtual menu system interacts with the parameter system through:
- `RenameParametersHook`: Adds postprocessors for parameter renaming
- `FixupExpressionsMenuPass`: Validates parameter names against the parameter list

### Reactive Components

Future reactive component systems may use the virtual menu system to:
- Generate menu items dynamically based on object states
- Apply parameter transformations for virtualized parameters

## Testing

The virtual menu system is tested through:

1. **Unit tests** (`VirtualMenuTests.cs`):
   - Empty menu handling
   - Basic menu construction
   - Cycle handling
   - Installer registration and targeting
   - Install target loops

2. **Integration tests**:
   - Full avatar builds with various menu configurations
   - Overflow splitting verification
   - Parameter renaming integration

3. **Manual testing**:
   - Editor preview UI (`MenuPreviewGUI`, `AvMenuTreeView`)
   - Real avatar builds with complex menu structures

## Summary

The Virtual Menu subsystem provides a powerful and flexible foundation for composing avatar menus in Modular Avatar. By representing menus as a mutable graph structure during the build process, it enables:

- Non-destructive menu composition from multiple sources
- Flexible parameter remapping through postprocessors
- Clean handling of cycles and complex menu structures
- Extensibility through the `MenuSource` interface

The system is designed to be simple enough for common use cases while providing the flexibility needed for advanced scenarios and future extensions.
