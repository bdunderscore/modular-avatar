using nadena.dev.modular_avatar;
using NUnit.Framework;

namespace nadena.dev.modular_avatar.test.Tests
{
    public class VersionTagTest
    {
        [TestCase("1.20.0-alpha.0", "1.19.0")]
        [TestCase("1.20.0-beta.1", "1.19.0")]
        [TestCase("1.20.0-rc.2", "1.19.0")]
        [TestCase("1.20.0", "1.20.0")]
        [TestCase("1.20.1-beta.0", "1.20.0")]
        [TestCase("1.0.0-beta.0", "1.0.0")]
        public void PrereleaseCarveoutOnlyLowersMinimumVersionForZeroPatchAndNonzeroMinor(
            string version, string expectedMinimumVersion)
        {
            var originalVersion = VersionTag.Current.UpdatedAtVersion;
            try
            {
                VersionTag.SetCurrentVersion(version);

                Assert.That(VersionTag.Current.UpdatedAtVersion, Is.EqualTo(version));
                Assert.That(VersionTag.Current.MinimumVersion, Is.EqualTo(expectedMinimumVersion));
            }
            finally
            {
                if (originalVersion != null) VersionTag.SetCurrentVersion(originalVersion);
            }
        }
    }
}
