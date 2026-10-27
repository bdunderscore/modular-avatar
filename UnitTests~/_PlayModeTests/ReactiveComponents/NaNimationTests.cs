using System.Collections;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.vertex_filters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace nadena.dev.modular_avatar.test._PlayModeTests.ReactiveComponents
{
    public class NaNimationTests : TestBase
    {
        private SkinnedMeshRenderer _body;
        private GameObject _outfit;
        private GameObject _controller;
        private Animator _animator;

        private Mesh CreateTwoPlaneMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                    new Vector3(0, 0, 2), new Vector3(1, 0, 2), new Vector3(0, 1, 2), new Vector3(1, 1, 2),
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 },
                boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 8).ToArray(),
                bindposes = new[] { Matrix4x4.identity }
            };
            var deltas = new Vector3[8];
            for (var i = 4; i < 8; i++) deltas[i] = new Vector3(0, 0.1f, 0);
            mesh.AddBlendShapeFrame("Key", 100, deltas, null, null);
            mesh.RecalculateBounds();
            return obj(mesh);
        }

        private void SetupAvatar(bool toggleActive)
        {
            var avatar = CreateRoot();

            var body = CreateChild(avatar, "Body");
            var bone = CreateChild(avatar, "BodyBone");
            _body = body.AddComponent<SkinnedMeshRenderer>();
            _body.sharedMesh = CreateTwoPlaneMesh();
            _body.bones = new[] { bone.transform };
            _body.rootBone = bone.transform;

            _outfit = CreateChild(avatar, "Outfit");
            _outfit.SetActive(!toggleActive);
            _controller = CreateChild(_outfit, "Controller");

            var menu = CreateChild(avatar, "Menu");
            var mi = menu.AddComponent<ModularAvatarMenuItem>();
            mi.PortableControl.Parameter = "toggle";
            var ot = menu.AddComponent<ModularAvatarObjectToggle>();
            ot.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(_outfit),
                Active = toggleActive
            });
        }

        private void Build(GameObject avatar)
        {
            LogAssert.ignoreFailingMessages = true;
            ProcessAvatar(avatar);
            _animator = ActivateFX(avatar);
        }

        private bool IsFarPlaneHidden()
        {
            var mesh = _body.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;

            return Enumerable.Range(0, vertices.Length)
                .Where(i => vertices[i].z > 1)
                .Any(i => float.IsNaN(_body.bones[weights[i].boneIndex0].lossyScale.x));
        }

        private IEnumerator AssertHiddenWhileOutfitActive(bool toggleActive)
        {
            foreach (var menuOn in new[] { false, true, false, true })
            {
                SetParam(_animator, "toggle", menuOn ? 1 : 0);
                for (var i = 0; i < 4; i++) yield return null;

                Assert.AreEqual(menuOn == toggleActive, _outfit.activeSelf, $"menuOn={menuOn}");
                Assert.AreEqual(_outfit.activeSelf, IsFarPlaneHidden(), $"menuOn={menuOn}");
            }
        }

        [UnityTest]
        public IEnumerator MeshCutterFollowsToggledParent([Values] bool toggleActive)
        {
            SetupAvatar(toggleActive);

            _controller.AddComponent<ModularAvatarMeshCutter>().Object = new AvatarObjectReference(_body.gameObject);
            var filter = _controller.AddComponent<VertexFilterByAxisComponent>();
            filter.Center = new Vector3(0, 0, 1);
            filter.Axis = Vector3.forward;

            Build(_body.transform.parent.gameObject);
            yield return AssertHiddenWhileOutfitActive(toggleActive);
        }

        [UnityTest]
        public IEnumerator ShapeDeletionFollowsToggledParent([Values] bool toggleActive)
        {
            SetupAvatar(toggleActive);

            var changer = _controller.AddComponent<ModularAvatarShapeChanger>();
            changer.Shapes.Add(new ChangedShape
            {
                Object = new AvatarObjectReference(_body.gameObject),
                ShapeName = "Key",
                ChangeType = ShapeChangeType.Delete,
                Value = 100
            });

            Build(_body.transform.parent.gameObject);
            yield return AssertHiddenWhileOutfitActive(toggleActive);
        }
    }
}
