﻿---
sidebar_position: 1
sidebar_label: 简单的衣装设置
---

# 简单的衣装设置

## 教程

使用 Modular Avatar，只需一键即可轻松导入简单的衣装。让我们来试一试！

本次教程将以かぷちや的[水手连衣裙](https://capettiya.booth.pm/items/3795694)为例，将其导入长兎路こより的[Anon-chan](https://booth.pm/ja/items/3564947)。我们假设你已经完成了VRCSDK和Modular Avatar本身的安装，以及各项资产的导入。

首先，将衣装预制件直接拖放到Avatar中。

![手順１](step1.png)

接下来，右键单击衣装对象，然后选择 **`[ModularAvatar] Setup Outfit`**。

![手順２](step2.png)

现在你可以按照自己喜欢的方式进行动画设置。通过打开/关闭 **`SailorOnepiece_Anon_PB`** 对象，可以控制整个衣装的显示/隐藏。为了测试，我们先手动关闭原始衣装。

进入播放模式后，你会看到衣装已经正确地跟随Avatar的骨架了。

![こいつ、動くぞッ！](it_moves.png)

如果你想移除衣装，直接从层级结构中删除它即可。

:::tip

使用 Modular Avatar，你无需解包原始的Avatar或衣装预制件！这使得在作者发布新版本时，更新Avatar和衣装变得更加容易！

:::

---

## 发生了什么？

当你选择 **`Setup Outfit`** 后，Modular Avatar 会自动找到衣装中的骨架，并为其添加一个 **`Merge Armature`** 组件。

![セットアップ後](after_setup.png)

该组件会在你进入播放模式的瞬间，自动将衣装的骨骼层级与原始Avatar的骨架进行合并。在合并过程中，它会最大限度地减少不必要的骨骼，并确保物理骨骼等活动组件保留在原位，让你无需复杂操作即可进行动画制作。

在播放模式下打开Avatar，可以看到合并后的状态。

![プレイモードのアーマチュア](play_mode_armature.png)

如你所见，新衣装独有的骨骼被添加到了Avatar的骨架中，而共享的骨骼则被合并到原始Avatar的骨架中。（你也可以移动和调整骨骼的位置，这没有问题！）

有点乱，但退出播放模式后，一切都会恢复到整洁的原始状态。

## Blendshape 同步

你还可以将衣装的 Blendshape 设置为与原始Avatar同步。首先，为需要同步 Blendshape 的对象添加 **`MA Blendshape Sync`** 组件。

![Adding the blendshape sync component](blendshape_1.png)

然后，点击 **`+`** 按钮。此时会弹出一个 Blendshape 选择窗口。展开折叠的项，找到原始Avatar中需要引用的网格和 Blendshape。

![Blendshape picker](blendshape_2.png)

双击你想要同步的 Blendshape，将其添加到 **`Blendshape Sync`** 组件中。你可以在“此网格的 Blendshape”中指定衣装的 Blendshape 名称，如果不填写，则默认使用原始Avatar的 Blendshape 名称。

![Completed setup](blendshape_3.png)

完成设置后，Modular Avatar 将自动同步 Blendshape 值，这不仅在编辑器和检查器中有效，在你上传Avatar后，通过动画操作 Blendshape 时也会同步。