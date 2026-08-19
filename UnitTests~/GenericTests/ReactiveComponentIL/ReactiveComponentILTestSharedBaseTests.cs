using System.Collections;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnitTests.SharedInterfacesImpl;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class ReactiveComponentILTestSharedBaseTests
    {
        [Test]
        public void InvokeTest_TearsDownAfterCoroutineCompletes()
        {
            CoroutineLifecycleProbe.Reset();

            var coroutine = ReactiveComponentILTestSharedBase.InvokeTest(
                nameof(CoroutineLifecycleProbe),
                nameof(CoroutineLifecycleProbe.Run)
            );

            Assert.IsTrue(coroutine.MoveNext());
            Assert.IsTrue(CoroutineLifecycleProbe.BodyStarted);
            Assert.IsFalse(CoroutineLifecycleProbe.TearDownCalled);

            Assert.IsFalse(coroutine.MoveNext());
            Assert.IsTrue(CoroutineLifecycleProbe.BodyCompleted);
            Assert.IsTrue(CoroutineLifecycleProbe.TearDownCalled);
        }

        [Test]
        public void TearDown_DestroysCreatedAvatar()
        {
            var fixture = new AvatarLifecycleProbe();
            fixture.SetUp();
            var avatar = fixture.Avatar;

            fixture.TearDown();

            Assert.IsTrue(avatar == null);
        }
    }

    public class CoroutineLifecycleProbe : ReactiveComponentILTestSharedBase
    {
        public static bool BodyStarted { get; private set; }
        public static bool BodyCompleted { get; private set; }
        public static bool TearDownCalled { get; private set; }

        public static void Reset()
        {
            BodyStarted = false;
            BodyCompleted = false;
            TearDownCalled = false;
        }

        [RCILTest]
        public IEnumerator Run()
        {
            BodyStarted = true;
            yield return null;
            BodyCompleted = true;
        }

        public override void SetUp()
        {
        }

        public override void TearDown()
        {
            TearDownCalled = true;
        }
    }

    public class AvatarLifecycleProbe : ReactiveComponentILTestBase
    {
        public GameObject Avatar => avatar;
    }
}
