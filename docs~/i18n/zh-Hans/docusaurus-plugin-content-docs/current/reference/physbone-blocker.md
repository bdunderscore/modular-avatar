# PhysBone Blocker



**PhysBone Blocker** 阻止以父对象为根的物理骨骼影响子对象。
它通过将此子对象添加到任何影响父对象的 PhysBone 的**忽略列表**中来实现。

## 我应该何时使用它？

当制作有人可能想附加到物理骨骼链（如尾巴或耳朵）上的配件时，你可以附加一个 **PhysBone Blocker** 以防止父物理骨骼链影响子对象。

请注意，你仍然可以向附有 **PhysBone Blocker** 的子对象本身附加一个 **PhysBone** 组件。

## 与 Bone Proxies 一起使用

当使用 **[Bone Proxy 组件](bone-proxy.md)** 将一个对象附加到现有的物理骨骼链上时，附加 **PhysBone Blocker** 将确保你的对象牢固地附加到父链上。
这样做时，最好将 **PhysBone Blocker** 放在与 **Bone Proxy** 相同的对象上。