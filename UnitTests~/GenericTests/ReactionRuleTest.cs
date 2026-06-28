using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;

namespace UnitTests.GenericTests
{
    public class ReactionRuleTest
    {
        private static ReactionRule MakeRule(bool inverted)
        {
            // No conditions → IsConstant is true, raw initially-active is true before inversion
            var rule = new ReactionRule(default, null);
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
            // Regression: operator precedence bug caused IsConstant && (InitiallyActive ^ Inverted)
            // to evaluate as true here, double-cancelling the inversion already baked into InitiallyActive.
            var rule = MakeRule(inverted: true);
            Assert.IsTrue(rule.IsConstant);
            Assert.IsFalse(rule.InitiallyActive);   // inversion already applied
            Assert.IsFalse(rule.IsConstantActive);  // was incorrectly true before fix
        }
    }
}
