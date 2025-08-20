﻿# Merge Animator

![Merge Animator](merge-animator.png)

**Merge Animator** 组件会将提供的动画控制器添加到所附加的Avatar的指定图层。这可以用来制作复杂的 AV3 小工具，只需拖放即可安装到Avatar上。

我们提供了两个使用此组件的**[示例](/docs/samples/)**：一个拍手效果和一个指尖笔小工具。

## 我应该何时使用它？

当你有一些想要作为小工具一部分播放的动画时，应该使用 **Merge Animator**。

## 我不应该何时使用它？

**Merge Animator** 是添加而不是替换现有的动画图层。如果你希望最终用户完全替换一个动画图层，最好让他们以传统方式在Avatar描述器中替换。

## 设置 Merge Animator

将 **Merge Animator** 组件添加到你预制件中的一个对象上，并将动画控制器附加到“要合并的动画控制器”字段中。将“图层类型”字段设置为应添加的Avatar图层（例如 FX）。

### 录制动画

默认情况下，动画控制器中的路径被解释为相对于 **Merge Animator** 组件的相对路径。这使得录制新动画变得容易，前提是你正在动画化 **Merge Animator** 组件下的对象。

只需将一个 **Animator** 组件附加到你的游戏对象上，你就可以使用“动画”面板来录制动画：

![Recording an animation using Merge Animator](merge-animator-record.png)

作为一种开发便利，你可以勾选“删除附加的动画控制器”复选框，以便在构建时移除 **Animator** 组件。

### 人形骨骼动画

移动人形骨骼的动画会忽略相对路径逻辑，始终应用于整个Avatar。因此，大多数人形动画（例如 AFK 动画）可以按原样使用。

### 路径模式

“路径模式”选项控制如何解释动画路径。在“相对”模式下，所有路径都相对于一个特定对象，通常是 **Merge Animator** 组件所附加的对象。这使你能够创建移动时也能工作的小工具，并通过使用 Unity 的动画控制器组件（如上所述）使其更容易录制动画。你可以通过设置“相对路径根”字段来控制哪个对象用作动画路径的根。

如果你想动画化已附加到Avatar上的对象（而不是在你对象下的），请将路径模式设置为“绝对”。这将使动画控制器使用绝对路径，并且不会尝试解释相对于 **Merge Animator** 组件的路径。
这意味着你需要使用Avatar的根动画控制器来录制你的动画。

### 图层优先级

图层优先级控制 **Merge Animator** 应用的顺序。它们将按优先级的递增顺序放置在最终的动画控制器中（也就是说，较低的数字排在动画控制器顺序的前面，较高的数字会覆盖它们）。具有相同优先级的 **Merge Animator** 将按照它们在层级结构中的顺序放置。任何预先存在的动画控制器都被视为优先级为零，排在所有优先级为零的 **Merge Animator** 之前。

### 合并模式

默认情况下，**Merge Animator** 会将动画控制器添加到指定的图层。如果你想替换图层，请将合并模式设置为“替换现有动画控制器”。这将用你提供的动画控制器替换在 VRChat Avatar描述器上配置的任何动画控制器。

被替换的动画控制器将保留你指定的优先级，但它会在该优先级级别的任何其他 **Merge Animator** 之前应用。

将多个 **Merge Animator** 设置为相同的图层类型和“替换”模式会导致错误。

### Write Defaults

默认情况下，你的动画控制器的 **Write Defaults** 状态不会被改变。如果你想确保你的动画控制器状态的 WD 设置始终与Avatar的动画控制器匹配，请点击“匹配Avatar Write Defaults”。
这将检测Avatar是否一致地使用 Write Defaults ON 或 OFF 状态，如果一致，你的动画控制器将被调整以匹配。如果Avatar使用 Write Defaults 的情况不一致，你的动画控制器将保持不变。

## 限制

### VRCAnimatorLayerControl

目前，**Merge Animator** 只支持引用同一动画控制器内图层的 **VRCAnimatorLayerControl** 状态行为。
如果你打算使用此功能，请确保 `Playable` 字段与 **Merge Animator** 组件上设置的图层匹配，并将 `Layer` 字段设置为你的动画控制器中的图层索引。