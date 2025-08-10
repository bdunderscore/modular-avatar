# MA Rename Collision Tags

`MA Rename Collision Tags` 组件允许你重命名 VRChat Contact 系统使用的碰撞标签，以避免与其他组件或小工具发生名称冲突。



## 我应该何时使用它？

当你有一些不希望与其他用户的虚拟形象发生干扰的触点，或者你想避免与自己虚拟形象上的其他组件或小工具发生名称冲突时，应该使用 `MA Rename Collision Tags` 组件。你也可以用它来配置多个小工具通过使用相同的标签名称相互作用。

## 如何使用它？

将 `MA Rename Collision Tags` 组件放置在你的虚拟形象中的任何游戏对象上，然后添加你想要重命名的标签。
你可以将标签设置为“自动重命名”，让 Modular Avatar 自动为你选择一个唯一的名称，或者你可以手动设置你想要的名称。同一游戏对象下的触点上的任何标签都会相应地被重命名。

请注意，将 `MA Rename Collision Tags` 组件放置在位于层级结构中其它位置的触点的 `Root Transform` 的父游戏对象上，并不会重命名该触点的标签。

你也可以在一个 `MA Rename Collision Tags` 组件下嵌套另一个 `MA Rename Collision Tags` 组件。在这种情况下，标签将首先根据子组件的设置进行重命名，然后父组件的设置将应用到第一轮重命名后的标签上。这使你可以创建重命名规则的层级结构，并用更简单的组件构建复杂的小工具。