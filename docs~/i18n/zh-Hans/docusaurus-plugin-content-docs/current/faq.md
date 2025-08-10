---
sidebar_position: 6
sidebar_label: FAQ
---

# 常见问题解答

## 我能用它导出到其他格式，比如 VRM 吗？

虽然 Modular Avatar 不会自动将转换应用到 UniVRM 或其他类似工具的导出中，但你可以先手动执行 Modular Avatar 转换，然后将生成的普通虚拟形象转换成 VRM。

要这样做，只需选择你的虚拟形象，然后从 Unity 菜单栏中选择 **Tools -> Modular Avatar -> Manual bake avatar**。
系统会创建一个已应用所有 Modular Avatar 转换的虚拟形象副本。之后，你就可以像往常一样使用 UniVRM。

:::caution

当你手动烘焙虚拟形象时，Modular Avatar 会生成一些网格和其它资产，但它们不会自动清理。
这些资产会被放置在一个名为 `ModularAvatarOutput` 的文件夹中。一旦你不再需要手动烘焙的虚拟形象，就可以随意删除它们。

:::

## 我可以用 Modular Avatar 合并不为我的虚拟形象设计的衣装吗？

可以，但有限制。Modular Avatar 假设原始虚拟形象和衣装的骨骼命名方式相同。如果不同，你需要重命名衣装的骨骼以进行匹配。

一旦你这样做，你就可以调整衣装骨骼的位置，这些调整将在 **Merge Armature** 运行时被保留。