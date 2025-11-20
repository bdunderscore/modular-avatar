using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnityEngine;

namespace UnitTests._PlayModeTests
{
    public class TestBase
    {
        [SetUp]
        public virtual void Setup()
        {
            _toDestroy.Clear();
            ITestSupport.Instance.Setup();
        }

        [TearDown]
        public virtual void Teardown()
        {
            foreach (var o in _toDestroy)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            ITestSupport.Instance.Teardown();
        }

        private List<UnityEngine.Object> _toDestroy = new();

        protected T obj<T>(T o) where T : UnityEngine.Object
        {
            _toDestroy.Add(o);
            return o;
        }
        
        protected GameObject CreateRoot()
        {
            return obj(ITestSupport.Instance.CreateTestAvatar(GetType().Name));
        }
        
        
        protected GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.parent = parent.transform;
            return obj(go);
        }

        protected GameObject CreatePrim(GameObject parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.parent = parent.transform;
            return obj(go);
        }
        
        protected GameObject CreatePrefab(string relPath)
        {
            var prefab = ITestSupport.Instance.LoadAsset<GameObject>(GetType(), relPath);

            return obj(Object.Instantiate(prefab));
        }

        protected void ProcessAvatar(GameObject avatar)
        {
            ITestSupport.Instance.ProcessAvatar(avatar);
        }

        protected Animator ActivateFX(GameObject avatar)
        {
            ITestSupport.Instance.ActivateFX(avatar);

            var anim = avatar.GetComponent<Animator>();
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return anim;
        }

        protected void SetParam(Animator animator, string name, float value)
        {
            var p = animator.parameters.First(p => p.name == name);

            switch (p.type)
            {
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(name, value > 0);
                    break;
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(name, value);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(name, (int) value);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    if (value > 0)
                    {
                        animator.SetTrigger(name);
                    }
                    else
                    {
                        animator.ResetTrigger(name);
                    }

                    break;
            }
        } 
    }
}