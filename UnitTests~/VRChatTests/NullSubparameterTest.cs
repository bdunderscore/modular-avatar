#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.Tests
{
    public class NullSubparameterTest : TestBase
    {
        [Test]
        public void TestNullSubparametersField()
        {
            VRCExpressionsMenu menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var root = CreateRoot("root");
    
            var avatar = root.GetComponent<VRCAvatarDescriptor>();
            avatar.expressionsMenu = menu;
    
            // This should not throw an exception
            ParameterPolicy.ProbeParameters(root);
        }
    }
}

#endif