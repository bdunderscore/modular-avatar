import ReactPlayer from 'react-player'

# Move Independently

<ReactPlayer controls muted loop playsinline url='/img/move-independently.mp4' />

**Move Independently** 组件允许你在不影响其子对象的情况下移动一个对象。
此组件在运行时没有任何效果；它仅用于编辑器。

## 我应该何时使用它？

此组件旨在用于调整衣装在虚拟形象上的契合度。例如，你可以调整衣装臀部对象的位置，而不影响其他对象的位置。

## 分组对象

通过勾选“要一起移动的对象”下的复选框，你可以创建一组一起移动的对象。
例如，你可以将臀部和上腿对象一起移动，而将下腿对象留在后面。

## 限制

虽然此组件支持独立于其子对象来缩放一个对象，但非均匀缩放（X、Y 和 Z 轴的缩放值不同）不完全支持，并可能导致意外行为。如果你需要独立调整每个轴的缩放，你应该同时使用 **[Scale Adjuster](scale-adjuster.md)** 组件和 **Move Independently**。