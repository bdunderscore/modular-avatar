#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

public class PreexistingParamsTest : TestBase
{
    [Test]
    public void TestPreexistingParameterOverwritePolicy()
    {
        var prefab = CreatePrefab("PreexistingParamsTest.prefab");
        AvatarProcessor.ProcessAvatar(prefab);

        var parameters = ((AnimatorController)FindFxController(prefab).animatorController).parameters;
        var paramDict = parameters.ToImmutableDictionary(p => p.name, p => p.defaultInt);

        foreach (var kvp in paramDict)
        {
            if (kvp.Key == "default_override" || kvp.Key == "animator_only")
            {
                Assert.AreEqual(1, kvp.Value);
            }
            else
            {
                Assert.AreEqual(11, kvp.Value);
            }
        }
    }
}

#endif