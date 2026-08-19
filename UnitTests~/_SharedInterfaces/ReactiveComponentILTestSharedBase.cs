using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;

namespace UnitTests.SharedInterfaces
{
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    // ReSharper disable once InconsistentNaming
    public class RCILTest : Attribute
    {
        
    }

    public abstract class ReactiveComponentILTestSharedBase
    {
        private static Type[] _allTestClasses;
        private static Type[] AllTestClasses
        {
            get
            {
                if (_allTestClasses != null) return _allTestClasses;
                
                _allTestClasses = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => !t.IsAbstract && typeof(ReactiveComponentILTestSharedBase).IsAssignableFrom(t))
                    .ToArray();

                return _allTestClasses;
            }
        }
        
        public static string[] TestNames(string name)
        {
            Debug.Log("Test class names: " + AllTestClasses.Select(t => t.Name).Aggregate((a, b) => a + ", " + b));
            
            return AllTestClasses.First(t => t.Name == name)
                ?.GetMethods()
                ?.Where(m => m.GetCustomAttribute<RCILTest>() != null)
                ?.Select(m => m.Name)
                ?.ToArray()
                ?? new string[] { "foo" };
        }

        public static IEnumerator InvokeTest(String name, string testName)
        {
            Debug.Log("Test class names: " + AllTestClasses.Select(t => t.Name).Aggregate((a, b) => a + ", " + b));

            var t = AllTestClasses.First(t => t.Name == name);
            var inst = (ReactiveComponentILTestSharedBase) t.GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());
            inst.SetUp();
            try
            {
                return (IEnumerator)t.GetMethod(testName)!.Invoke(inst, Array.Empty<object>());
            }
            finally
            {
                inst.TearDown();
            }
        }

        public abstract void SetUp();
        public abstract void TearDown();
    }
}