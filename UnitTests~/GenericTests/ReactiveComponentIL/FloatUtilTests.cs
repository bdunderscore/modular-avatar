using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class FloatUtilTests
    {
        [Test]
        public void NextLargest_HandlesSpecialValuesAndSignedZero()
        {
            Assert.IsTrue(float.IsNaN(float.NaN.NextLargest()));
            Assert.AreEqual(float.PositiveInfinity, float.PositiveInfinity.NextLargest());
            Assert.AreEqual(-float.MaxValue, float.NegativeInfinity.NextLargest());

            Assert.AreEqual(float.Epsilon, 0f.NextLargest());
            Assert.AreEqual(float.Epsilon, (-0f).NextLargest());
        }

        [Test]
        public void NextSmallest_MirrorsNextLargestAtSpecialValueBoundaries()
        {
            Assert.IsTrue(float.IsNaN(float.NaN.NextSmallest()));
            Assert.AreEqual(float.MaxValue, float.PositiveInfinity.NextSmallest());
            Assert.AreEqual(float.NegativeInfinity, float.NegativeInfinity.NextSmallest());

            Assert.AreEqual(-float.Epsilon, 0f.NextSmallest());
            Assert.AreEqual(-float.Epsilon, (-0f).NextSmallest());
        }

        [TestCase(-1f, -0.99999994f, -1.00000012f)]
        [TestCase(1f, 1.00000012f, 0.99999994f)]
        public void NextLargestAndSmallest_ReturnKnownAdjacentFiniteValues(
            float value, float nextLargest, float nextSmallest
        )
        {
            Assert.AreEqual(nextLargest, value.NextLargest());
            Assert.AreEqual(nextSmallest, value.NextSmallest());
        }
    }
}
