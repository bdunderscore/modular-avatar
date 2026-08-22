using System.Reflection;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

using UnityEditor.UIElements;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;
public class ParameterNameAssignmentTests : TestBase
{
    [Test]
    public void MenuItemsWithChildRC_CreateParameterOnAnimator()
    {
        var prefab = CreatePrefab("MenuItemsWithChildRC_CreateParameterOnAnimator.prefab");

        AvatarProcessor.ProcessAvatar(prefab);

        var fx = (AnimatorController)FindFxController(prefab).animatorController;
        var expMenu = prefab.GetComponent<VRCAvatarDescriptor>().expressionsMenu;

        var toggleParam = expMenu.controls.Find(c => c.name == "toggle").parameter.name;
        Assert.IsTrue(fx.parameters.Any(p => p.name == toggleParam && p.type == AnimatorControllerParameterType.Float));
    }

    [Test]
    public void AddingDetectedParameterThroughInspectorCanBeUndone()
    {
        var root = CreateRoot("root");
        var parameters = root.AddComponent<ModularAvatarParameters>();
        var menuItem = CreateChild(root, "Menu Item").AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Parameter = "Detected";

        var editor = UnityEditor.Editor.CreateEditor(parameters, typeof(AvatarParametersEditor));
        editor.CreateInspectorGUI();
        InvokePrivate(editor, "DetectParameters");

        var detectedList = (ListView)GetPrivateField(editor, "unregisteredListView");
        var row = new VisualElement();
        detectedList.bindItem(row, 0);
        var addButton = row.Q<Button>();
        Assert.NotNull(addButton);

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        InvokeButton(addButton);
        Undo.CollapseUndoOperations(undoGroup);

        Assert.That(parameters.parameters, Has.Count.EqualTo(1));
        Assert.AreEqual("Detected", parameters.parameters[0].nameOrPrefix);

        Undo.PerformUndo();

        Assert.That(parameters.parameters, Is.Empty);
        Object.DestroyImmediate(editor);
    }


    [Test]
    public void ImportingParametersIntoPrefabInstanceCanBeUndoneAndPersistsOverride()
    {
        var template = new GameObject("Parameter prefab");
        template.AddComponent<ModularAvatarParameters>();
        AssetDatabase.CreateFolder("Assets", "ZZZ_Temp");
        var prefab = PrefabUtility.SaveAsPrefabAsset(template, "Assets/ZZZ_Temp/Parameter prefab.prefab");
        Object.DestroyImmediate(template);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        TrackObject(instance);
        var instanceParameters = instance.GetComponent<ModularAvatarParameters>();
        var source = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
        source.parameters = new[]
        {
            new VRCExpressionParameters.Parameter
            {
                name = "Imported",
                valueType = VRCExpressionParameters.ValueType.Int,
                networkSynced = true,
                saved = true,
            }
        };

        var editor = UnityEditor.Editor.CreateEditor(instanceParameters, typeof(AvatarParametersEditor));
        var importField = new ObjectField { value = source };

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        InvokePrivate(editor, "ImportValues", importField);
        Undo.CollapseUndoOperations(undoGroup);

        Assert.That(instanceParameters.parameters, Has.Count.EqualTo(1));
        Assert.AreEqual("Imported", instanceParameters.parameters[0].nameOrPrefix);
        Assert.True(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false));

        Undo.PerformUndo();
        Assert.That(instanceParameters.parameters, Is.Empty);
        Undo.PerformRedo();

        PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
        var persisted = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZZZ_Temp/Parameter prefab.prefab")
            .GetComponent<ModularAvatarParameters>();
        Assert.That(persisted.parameters, Has.Count.EqualTo(1));
        Assert.AreEqual("Imported", persisted.parameters[0].nameOrPrefix);

        Object.DestroyImmediate(editor);
    }

    private static object GetPrivateField(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
    }

    private static void InvokePrivate(object instance, string name, params object[] arguments)
    {
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(instance, arguments);
    }

    private static void InvokeButton(Button button)
    {
        var invoke = button.clickable.GetType().GetMethod(
            "Invoke",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        Assert.NotNull(invoke);
        invoke.Invoke(button.clickable, new object[] { ClickEvent.GetPooled() });
    }
}
