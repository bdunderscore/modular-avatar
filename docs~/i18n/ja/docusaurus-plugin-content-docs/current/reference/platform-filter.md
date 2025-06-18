# プラットフォームフィルター

Platform Filter（プラットフォームフィルター）コンポーネントは、ビルド先となるVRSNSプラットフォーム（例：VRChat、Resoniteなど）
に応じて、特定のGameObjectをアバターから除外することができます。

## いつ使うの？

特定のオブジェクトやコンポーネントを特定のプラットフォームでのみ存在させたい、あるいは特定のプラットフォームから排除したい
場合にPlatform Filterを使用します。例えば、VRChat専用のギミックをVRChatでのみ含めたい場合などに便利です。

## 使わなくてもいい場合は？

多くのModular Avatarの機能は、すでにプラットフォーム固有の制限に対応しています。例えば、Merge AnimatorはVRChatでのみ
動作します。そのため、Platform Filterを追加する必要がない場合もあります。

## Platform Filterの手動設定

Platform Filterコンポーネントを、フィルターしたいGameObjectに追加します。同じGameObjectに複数のPlatform Filter
コンポーネントを追加して、複数のプラットフォームを指定することもできます。各フィルターは「**Include（含める）**」
または「**Exclude（除外する）**」のいずれかに設定できます：

- **Include（含める）**: 指定したプラットフォームでのみGameObjectが存在します。
- **Exclude（除外する）**: 指定したプラットフォームでGameObjectが削除されます。

同じGameObjectに「含める」と「除外する」フィルターが両方設定されている場合、エラーになります。

## 使用例

- オブジェクトをVRChatでのみ表示したい場合は、Platform Filterを追加し、Platformを「VRChat」、モードを**Include**に設定します。
- オブジェクトをResoniteで非表示にしたい場合は、Platform Filterを追加し、Platformを「Resonite」、モードを**Exclude**に設定します。

