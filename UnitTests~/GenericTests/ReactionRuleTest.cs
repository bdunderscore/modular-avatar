using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using NUnit.Framework;

namespace UnitTests.GenericTests
{
    public class ReactionRuleTest
    {
        private static ReactionRule MakeRule(bool inverted)
        {
            // No conditions → IsConstant is true, raw initially-active is true before inversion
            var rule = new ReactionRule(new NullAction());
            rule.Inverted = inverted;
            return rule;
        }

        [Test]
        public void IsConstantActive_NoConditions_NotInverted_IsTrue()
        {
            var rule = MakeRule(inverted: false);
            Assert.IsTrue(rule.IsConstant);
            Assert.IsTrue(rule.InitiallyActive);
            Assert.IsTrue(rule.IsConstantActive);
        }

        [Test]
        public void IsConstantActive_NoConditions_Inverted_IsFalse()
        {
            // Regression: `IsConstant && InitiallyActive ^ Inverted` was parsed as
            // `(IsConstant && InitiallyActive) ^ Inverted`, and `InitiallyActive` already includes inversion.
            var rule = MakeRule(inverted: true);
            Assert.IsTrue(rule.IsConstant);
            Assert.IsFalse(rule.InitiallyActive);   // inversion already applied
            Assert.IsFalse(rule.IsConstantActive);  // was incorrectly true before fix
        }

        [Test]
        public void TryMerge_SetShapeKeyValuesWithinTolerance_Merges()
        {
            var first = new ReactionRule(new SetShapeKey(null, "shape", 12.5f));
            var second = new ReactionRule(new SetShapeKey(null, "shape", 12.5005f));

            Assert.IsTrue(first.TryMerge(second));
        }

        [Test]
        public void TryMerge_SetShapeKeyValuesOutsideTolerance_DoesNotMerge()
        {
            var first = new ReactionRule(new SetShapeKey(null, "shape", 12.5f));
            var second = new ReactionRule(new SetShapeKey(null, "shape", 12.502f));

            Assert.IsFalse(first.TryMerge(second));
        }

        [Test]
        public void TryMerge_NonShapeActionsRequireExactEquality()
        {
            var first = new ReactionRule(new DriveParameter("parameter", 1f));
            var same = new ReactionRule(new DriveParameter("parameter", 1f));
            var different = new ReactionRule(new DriveParameter("parameter", 1.0005f));

            Assert.IsTrue(first.TryMerge(same));
            Assert.IsFalse(first.TryMerge(different));
        }
    }
}
