﻿---
sidebar_position: 4
---

# 进阶开关

在开始之前，请确保你已经阅读了 **[简单的对象开关教程](/docs/tutorials/object_toggle/)**。
本教程将在此基础上，向你展示如何在开关对象时也更新 Blendshape。

首先，了解一些背景知识。许多Avatar都带有“缩减形状键”（shrink blendshapes），它们用于隐藏Avatar的一部分，以防止其穿透衣服。这些形状键通常与开关功能结合使用，用于隐藏和显示衣物。

使用 Modular Avatar 的 [`Shape Changer`](/docs/reference/reaction/shape-changer/) 组件，你可以轻松地在开关对象时更新这些形状键。让我们以 Anon-chan 的袜子和鞋子为例。

<div style={{"display": "flex", "flex-direction": "row"}}>
<figure>
![靴ありのあのんちゃん](0a-boots-on.png)
<figcaption>鞋子穿上</figcaption>
</figure>

<figure>
![靴OFF](0b-boots-off.png)
<figcaption>鞋子脱下</figcaption>
</figure>
</div>

如你所见，当鞋子被关闭时，可以看到一小段突出的部分。这是因为袜子和下面的脚都通过缩减形状键隐藏了。我们先将它们重置为零。

<div style={{"display": "flex", "flex-direction": "row"}}>
<figure>
![Anon_body のブレンドシェープ](1a-bs-settings.png)
<figcaption>Anon_body 的形状键</figcaption>
</figure>

<figure>
![ソックスのブレンドシェープ](1b-bs-settings.png)
<figcaption>袜子的形状键</figcaption>
</figure>
</div>

<figure>
![リセット後](1c-results.png)
<figcaption>形状键重置为零后的效果</figcaption>
</figure>

现在，让我们设置 **Shape Changer** 来开关这些隐藏的图层。我们会在鞋子和袜子对象上各添加一个，并缩小下面的图层。

<div style={{"display": "flex", "flex-direction": "row"}}>
<figure>
![靴に付属するShape Changer](2a-sc-boots.png)
<figcaption>鞋子的 Shape Changer</figcaption>
</figure>

<figure>
![靴下に付属するShape Changer](2b-sc-socks.png)
<figcaption>袜子的 Shape Changer</figcaption>
</figure>
</div>

注意，我们在这里使用了 `Delete` 模式。这是因为，如果没有可动画化的开关，Modular Avatar 会自动移除底层多边形而不是缩小它们，这可以带来性能优势。如果你有任何可以动画化该对象的动画，它会改为将形状设置为 100。如果你希望在所有情况下都保留底层多边形，可以使用 `Set` 模式。

**Shape Changer** 会在编辑器中预览其效果……但如果我们设置得正确，很难看出它在工作！要检查，点击调试叠加按钮，选择 `Overdraw`，以获得透视视图，观察下面发生了什么。不过，如果你缩小太多，在场景视图中会立即显而易见。

<div style={{"display": "flex", "flex-direction": "row"}}>
<figure style={{"width": "100%"}}>
![Overdraw 設定の位置](3a-overdraw-menu.png)
<figcaption>过度绘制调试视图</figcaption>
</figure>

<figure>
![Overdraw にするとこんな感じです](3b-overdraw.png)
<figcaption>过度绘制视图</figcaption>
</figure>
</div>

既然我们已经设置了 **Shape Changer**，接下来就可以设置我们的开关了。我们先创建一个子菜单。
创建一个新的游戏对象，并添加 **`Menu Installer`** 和 **`Menu Item`**。然后将菜单项类型设置为 **`Submenu`**。

<div style={{"display": "flex", "flex-direction": "row", "justify-content": "center"}}>
<figure>
![Submenu 設定](4-submenu-setup.png)
<figcaption>子菜单设置</figcaption>
</figure>
</div>

现在，点击“添加开关”来在这个子菜单下创建一个新开关。给它一个新名称，并将我们的鞋子对象添加到其中。袜子也做同样的操作。

<div style={{"display": "flex", "flex-direction": "row", "justify-content": "center"}}>
<figure>
![靴のトグル設定](5-toggle-boots.png)
<figcaption>鞋子的开关设置</figcaption>
</figure>

<figure>
![靴下のトグル設定](5b-toggle-socks.png)
<figcaption>袜子的开关设置</figcaption>
</figure>
</div>

:::warning

虽然我们可以通过点击菜单项上的 `Default` 复选框来预览这些开关的效果，但在当前版本的 Modular Avatar 中，开关对形状键的下游影响无法在编辑器中预览。请使用 **Avatar 3.0 Emulator** 或 **Gesture Manager** 在播放模式下进行测试。

这个限制将在未来的版本中得到改善。

:::

大功告成！如你所见，Modular Avatar 的反应式组件系统旨在让你轻松设置缩减形状键和其他常见的衣装设置。

:::tip

衣装创作者可以预设这些 **Shape Changer**，以便轻松安装衣物，并自动配置形状键。反应式组件系统会响应其他兼容 NDMF 的系统创建的动画，因此你的用户不一定需要使用 Modular Avatar 的开关系统来获得这些形状键的好处。

:::