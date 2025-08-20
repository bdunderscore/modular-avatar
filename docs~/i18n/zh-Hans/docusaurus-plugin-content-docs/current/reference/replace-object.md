﻿# Replace Object

![Replace Object](replace-object.png)

**Replace Object** 组件允许你完全替换父Avatar上的一个游戏对象的内容。

## 我应该何时使用它？

**Replace Object** 组件在你想要用一个不同的对象替换父Avatar上的对象时非常有用。例如，你可能想要替换基础Avatar的 PhysBones 配置，或者用一个不同的网格完全替换其身体网格。

## 我不应该何时使用它？

一个对象只能被另一个对象替换。因此，当你使用 **Replace Object** 时，你的资产与可能也想使用 **Replace Object** 的其他资产的兼容性会受到限制。

## 详细操作

### 子对象的处理

**Replace Object** 只替换指定的特定对象。原始对象和替换对象的子对象都将被放置在替换对象之下。

### 对象命名

**Replace Object** 不会更改你的替换对象的名称；如果它的名称与原始对象不同，那么最终的对象名称也会不同。但是，**Replace Object** 会更新任何引用原始对象的动画路径，使其引用替换对象。

由于 **Replace Object** 在Avatar处理的后期才执行，在大多数情况下这不会产生太大的影响。但是，如果你正在替换 `Body` 网格并想要保持 MMD 世界兼容性（或者反过来，想要向现有Avatar添加 MMD 兼容性），这可能会很重要。

### 组件引用的处理

**Replace Object** 会尝试修复任何指向旧对象上组件的引用，使其指向新对象。
如果旧对象上有多个相同组件，则引用将与新对象中相同类型且索引相同的组件进行匹配（如果新对象中没有足够的该类型组件，则引用将变为空）。

**Replace Object** 不会执行模糊匹配；例如，如果你用一个 **Sphere Collider** 替换了一个 **Box Collider**，那么指向旧 **Box Collider** 的引用将变为空。