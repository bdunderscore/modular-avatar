using nadena.dev.modular_avatar;
using NUnit.Framework;

namespace modular_avatar_tests
{
    public class SemVerComparatorTest
    {
        private static readonly SemVerComparator Comparator = new();

        [TestCase("1.0.0", "2.0.0")]
        [TestCase("2.0.0", "2.1.0")]
        [TestCase("2.1.0", "2.1.1")]
        [TestCase("1.0.0-alpha", "1.0.0-alpha.1")]
        [TestCase("1.0.0-alpha.1", "1.0.0-alpha.beta")]
        [TestCase("1.0.0-alpha.beta", "1.0.0-beta")]
        [TestCase("1.0.0-beta", "1.0.0-beta.2")]
        [TestCase("1.0.0-beta.2", "1.0.0-beta.11")]
        [TestCase("1.0.0-rc.1", "1.0.0")]
        public void OrdersSemanticVersions(string lower, string higher)
        {
            Assert.That(Comparator.Compare(lower, higher), Is.LessThan(0));
            Assert.That(Comparator.Compare(higher, lower), Is.GreaterThan(0));
        }

        [Test]
        public void IgnoresBuildMetadata()
        {
            Assert.That(Comparator.Compare("1.2.3+build.1", "1.2.3+build.2"), Is.Zero);
            Assert.That(Comparator.Compare("1.2.3-rc.1+build.1", "1.2.3-rc.1"), Is.Zero);
        }

        [TestCase("1.2.3-alpha", "1.2.3")]
        [TestCase("1.2.3-beta.1", "1.2.3")]
        [TestCase("1.2.3-alpha", "1.2.3-rc.1")]
        public void TreatsPrereleasesAsCompatibleWithTheirBaseVersion(string first, string second)
        {
            Assert.That(Comparator.CompareForCompatibility(first, second), Is.Zero);
            Assert.That(Comparator.CompareForCompatibility(second, first), Is.Zero);
        }

        [TestCase("1.2")]
        [TestCase("01.2.3")]
        [TestCase("1.2.3-01")]
        [TestCase("1.2.3+")]
        [TestCase("1.2.3-rc..1")]
        public void TreatsInvalidSemanticVersionsAsZero(string version)
        {
            Assert.That(Comparator.Compare(version, "0.0.0"), Is.Zero);
            Assert.That(Comparator.Compare("0.0.0", version), Is.Zero);
            Assert.That(Comparator.CompareForCompatibility(version, "0.0.0"), Is.Zero);
            Assert.That(Comparator.CompareForCompatibility("0.0.0", version), Is.Zero);
        }
    }
}
