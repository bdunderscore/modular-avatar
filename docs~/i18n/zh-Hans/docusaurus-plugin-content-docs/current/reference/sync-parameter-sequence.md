﻿# Sync Parameter Sequence

![Sync Parameter Sequence](sync-parameter-sequence.png)

在 VRChat 中，需要在不同平台（如 PC 和 Android）之间共享的参数，必须以相同的顺序出现在表情参数列表的开头。此组件会调整你的表情参数的顺序，并在必要时添加额外参数，以确保你的Avatar在 PC 和 Android 之间正确同步。

## 我应该何时使用它？

如果你正在为同一个Avatar上传不同版本到 PC 和 Android，并且两个版本都使用了同步表情参数，那么你应该使用此组件。

## 我不应该何时使用它？

此组件可能与某些 VRCFury 组件（如 Parameter Compressor）存在兼容性问题。

## 如何使用它？

首先，将 **Sync Parameter Sequence** 组件附加到你的Avatar的任何对象上。然后，点击“新建”按钮创建一个资产来保存参数序列。在你的Avatar的其他平台变体上，附加该组件并选择你刚刚创建的资产。在 Android（或你希望作为主要平台的任何平台）上上传，然后再为其他平台上传。

每当你在被列为“主要平台”的平台上上传你的Avatar时，Modular Avatar 都会将其表情参数记录在此资产中。然后，当你稍后在其他平台上传时，Modular Avatar 会调整参数的顺序以匹配主要平台。

## 参数限制

**Sync Parameter Sequence** 组件会在必要时向你的Avatar添加额外参数，以确保参数顺序在平台之间匹配。这可能导致你的Avatar超出最大参数数量，从而导致构建失败。

为了解决这个问题，你可以清除参数资产的内容以清除过时的参数；或者，确保你没有大量仅限 Android 和仅限 PC 的参数，因为你最终会使用两者的组合，从而超过限制。