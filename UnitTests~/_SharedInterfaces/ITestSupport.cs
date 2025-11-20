#nullable enable

using System;
using UnityEngine;

namespace UnitTests.SharedInterfaces
{
    public interface ITestSupport
    {
        private static ITestSupport? _instance;

        public static ITestSupport Instance
        {
            get => _instance ?? throw new Exception("Test support not initialized");
            set
            {
                if (_instance != null) throw new Exception("Test support already initialized");
                _instance = value;
            }
        }

        public void Setup()
        {
        }

        public void Teardown()
        {
        }

        public void ProcessAvatar(GameObject gameObject);
        public T? LoadAsset<T>(Type relativeType, string path) where T : UnityEngine.Object;
        public GameObject CreateTestAvatar(string name);
        void ActivateFX(GameObject avatar);
    }
}