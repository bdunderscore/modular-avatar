# World Scale Object

此组件可用于强制一个游戏对象保持与世界相同的比例，无论当前Avatar的比例如何。它将为游戏对象附加一个 **ScaleConstraint** 或 **VRCScaleConstraint**，并将约束设置为相对于世界保持 1,1,1 的比例。

## 我应该何时使用它？

当你希望一个游戏对象与世界而不是Avatar一起缩放时。这在某些复杂的约束小工具中可能很有用。

## 设置 World Scale Object

只需将 `World Scale Object` 组件附加到一个游戏对象上。没有可配置的选项。

请注意，`World Scale Object` 目前在 Unity 编辑器中无法预览，但在游戏中或播放模式下可以正常工作。