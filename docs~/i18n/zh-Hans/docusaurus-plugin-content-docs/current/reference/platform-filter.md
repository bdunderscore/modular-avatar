# Platform Filter

**Platform Filter** 组件允许你根据目标 VRSNS 平台（如 VRChat, Resonite 等）来包含或排除你的Avatar中的特定游戏对象。

## 我应该何时使用它？

当你希望某些对象或组件仅存在于特定平台上时，可以使用 **Platform Filter**。例如，你可能希望仅在 VRChat 上包含一个仅限 VRChat 的小工具。

## 我不应该何时使用它？

许多 Modular Avatar 功能已经处理了平台特定的限制。例如，**Merge Animator** 仅在 VRChat 上运行。因此，并非总是需要添加 **Platform Filter**。

## 手动配置 Platform Filter

将 **Platform Filter** 组件添加到你希望过滤的任何游戏对象上。你可以向同一个游戏对象添加多个 **Platform Filter** 组件来指定多个平台。每个过滤器都可以设置为**包含**或**排除**一个平台：

- **包含**：该游戏对象仅在为指定的平台构建时存在。
- **排除**：该游戏对象将在指定的平台上被移除。

如果一个游戏对象同时具有包含和排除过滤器，则会报告错误。

## 使用示例

- 要使一个对象仅在 VRChat 上出现，请添加一个 **Platform Filter**，将平台设置为“VRChat”，并将其模式设置为**包含**。
- 要在 Resonite 上隐藏一个对象，请添加一个 **Platform Filter**，将平台设置为“Resonite”，并将其模式设置为**排除**。