# Remove Vertex Color

![Remove Vertex Color](remove-vertex-color.png)

Remove Vertex Color コンポーネントは、アタッチされたオブジェクトとその子オブジェクトから頂点カラーを削除します。

## いつ使うものか？

時々、アバターや衣装には意図されていない頂点カラーが付いていることがあります。VRChat Mobile系統など頂点カラーを使用するシェーダーに変更すると、
変色してしまうことがあります。このコンポーネントを使えば、非破壊的に問題の頂点カラーを削除できます。

<div style={{display: "flex", "flex-direction": "row"}}>
    <div style={{margin: "1em"}}>
        <div>
        ![不要な頂点カラーがある場合](remove-vertex-color-before.png)
        </div>
        *Remove Vertex Color を使わないと、このアバターの髪の毛に不要な頂点カラーで変色してしまいます。*
    </div>
    <div style={{margin: "1em"}}>
        <div>
        ![頂点カラーを削除した後](remove-vertex-color-after.png)
        </div>
        *Remove Vertex Color を追加した後、アバターの髪の色が正しくなります。*
    </div>
</div>

## 詳細な使い方

Remove Vertex Color コンポーネントをアバターのオブジェクトに追加してください。通常、ルートオブジェクトに追加するだけで十分です。
このオブジェクト以下のすべてのオブジェクトの頂点カラーが削除されます。

特定のオブジェクトを除外したい場合は、除外したいオブジェクトに Remove Vertex Color コンポーネントを追加し、モードを「頂点カラーを削除しない」
に設定してください。このオブジェクト以下のオブジェクトの頂点カラーは削除されません。
