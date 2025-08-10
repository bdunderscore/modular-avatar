---
sidebar_position: 7
---

# 手动处理

在为 VRChat 开发时，通常可以让 Modular Avatar 自动处理你的虚拟形象；当你进入播放模式或构建虚拟形象时，Modular Avatar 会自动应用其转换。然而，在某些情况下，你可能需要手动应用 Modular Avatar 的处理——例如，为非 VRChat 平台构建虚拟形象，或者在调试虚拟形象问题时。

你可以通过右键点击你的虚拟形象，然后选择 `Modular Avatar -> Manual bake avatar` 来触发手动处理。Modular Avatar 会创建一个已应用所有转换的虚拟形象副本。



## 生成的资产

Modular Avatar 会根据你附加到虚拟形象上的组件生成一些资产。当你手动烘焙虚拟形象时，这些资产会保存在你项目主 Assets 文件夹下的一个名为 `ModularAvatarOutput` 的文件夹中。
最初，所有资产都打包在一个文件中；这是为了避免 Unity Bug 并缩短处理时间。
不过，你可以在项目视图中选择该文件，然后点击检查器面板上的 `Unpack` 来解包。



然后，Modular Avatar 会将生成的资产解包为单独的文件。

无论你选择解包与否，如果你删除了克隆的烘焙虚拟形象，删除 `ModularAvatarOutput` 文件是安全的。