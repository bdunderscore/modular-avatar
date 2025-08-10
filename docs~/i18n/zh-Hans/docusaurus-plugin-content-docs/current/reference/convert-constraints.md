# Convert Constraints

**Convert Constraints** 组件指示 Modular Avatar 在构建时非破坏性地将 Unity 约束转换为 VRChat 约束。它会转换其所附加的对象及其所有子对象上的任何约束（以及引用它们的动画）。它还会尝试修复使用旧版 Modular Avatar 时，因 VRCSDK 的 **Auto Fix** 按钮而损坏的动画。

## 我应该何时使用它？

在大多数情况下，将此组件放在虚拟形象根部是个好主意，因为预先转换约束可以显著提高性能。当安装了 MA 后，如果你的虚拟形象根部还没有这个组件，VRChat 的 **Auto Fix** 按钮会自动添加它。

## 我不应该何时使用它？

此组件主要是为了让用户在怀疑它导致问题时，可以通过删除此组件来禁用此功能。