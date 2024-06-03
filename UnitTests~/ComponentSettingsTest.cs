using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Activator = nadena.dev.modular_avatar.core.Activator;
using Object = UnityEngine.Object;

namespace modular_avatar_tests
{
    public class ComponentSettingsTest : TestBase
    {
        GameObject _gameObject;
        Texture2D _iconTexture;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _gameObject = new GameObject();
            _iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                AssetDatabase.GUIDToAssetPath("a8edd5bd1a0a64a40aa99cc09fb5f198"));
        }

        public override void Teardown()
        {
            base.Teardown();
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        [TestCaseSource(nameof(ComponentTypes))]
        public void CheckDisallowMultipleComponentIsSpecified(Type type)
        {
            // excluded types
            if (type == typeof(Activator)) return;
            if (type == typeof(AvatarActivator)) return;
            if (type == typeof(TestComponent)) return;
            if (type == typeof(ModularAvatarShapeChanger)) return;

            // get icon
            var component = (MonoBehaviour) _gameObject.AddComponent(type);
            var monoScript = MonoScript.FromMonoBehaviour(component);
            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
            var monoImporter = (MonoImporter) AssetImporter.GetAtPath(scriptPath);
            // in Unity 2021.2, we can use monoImporter.GetIcon()
            // but it's not available in unity 2019 so use SerializedObject
            var serializedImporter = new SerializedObject(monoImporter);
            var iconProperty = serializedImporter.FindProperty("icon");
            var icon = iconProperty.objectReferenceValue;

            // check the icon
            Assert.That(icon, Is.EqualTo(_iconTexture));
        }

        [Test]
        [TestCaseSource(nameof(ComponentTypes))]
        public void CheckHelpURL(Type type)
        {
            // excluded types
            if (type == typeof(Activator)) return;
            if (type == typeof(AvatarActivator)) return;
            if (type == typeof(TestComponent)) return;

            // get icon
            var helpUrl = type.GetCustomAttribute<HelpURLAttribute>();
            Assert.That(helpUrl, Is.Not.Null);
        }

        /// <returns>All non-abstract MonoBehaviour classes</returns>
        static IEnumerable<Type> ComponentTypes()
        {
            return
                typeof(AvatarTagComponent).Assembly
                    .GetTypes()
                    .Where(x => x.IsClass && !x.IsAbstract)
                    .Where(x => typeof(MonoBehaviour).IsAssignableFrom(x));
        }
    }
}