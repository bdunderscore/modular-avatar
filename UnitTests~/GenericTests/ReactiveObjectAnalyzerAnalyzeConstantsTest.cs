using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    /// <summary>
    /// Regression test: An off-by-one error resulted in AnalyzeConstants leaving one extra dead group when there are
    /// dead groups before the last always-on group.
    /// </summary>
    public class ReactiveObjectAnalyzerAnalyzeConstantsTest : TestBase
    {
        /// <summary>
        /// When there are dead groups before the last always-on group, AnalyzeConstants should
        /// remove ALL of them, not one fewer. The off-by-one leaves a redundant dead group.
        /// </summary>
        [Test]
        public void AnalyzeConstants_RemovesAllDeadGroupsBeforeLastAlwaysOn()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            // ReadablePropertyExtension depends on AnimatorServicesContext
            buildContext.ActivateExtensionContextRecursive<ReadablePropertyExtension>();

            var analyzer = new ReactiveObjectAnalyzer(buildContext);

            // Create a target property for the action groups
            var targetObj = CreateChild(root, "target");
            var targetProp = TargetProp.ForObjectActive(targetObj);

            // Create 3 action groups:
            // - rule0: IsConstantActive = false (dead group, condition with IsConstant = false)
            // - rule1: IsConstantActive = false (dead group, condition with IsConstant = false)
            // - rule2: IsConstantActive = true (always-on group, condition with IsConstant = true)
            var animatedProperty = new AnimatedProperty(targetProp, 1.0f);

            // Dead rule 0: condition is not constant → IsConstantActive = false
            var rule0 = CreateRuleWithCondition(isConstant: false, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule0);

            // Dead rule 1: condition is not constant → IsConstantActive = false
            var rule1 = CreateRuleWithCondition(isConstant: false, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule1);

            // Always-on rule 2: condition is constant and initially active → IsConstantActive = true
            var rule2 = CreateRuleWithCondition(isConstant: true, initiallyActive: true, inverted: false);
            animatedProperty.actionGroups.Add(rule2);

            var shapes = new Dictionary<TargetProp, AnimatedProperty>
            {
                { targetProp, animatedProperty }
            };

            analyzer.AnalyzeConstants(shapes);

            // After AnalyzeConstants:
            // - lastAlwaysOnGroup should be 2 (index of rule2, the only IsConstantActive=true)
            // - RemoveRange(0, lastAlwaysOnGroup) = RemoveRange(0, 2) should remove rule0 and rule1
            // - Only rule2 should remain
            //
            // BUG: The code does RemoveRange(0, lastAlwaysOnGroup - 1) = RemoveRange(0, 1)
            // which only removes rule0, leaving rule1 and rule2 (2 groups instead of 1).
            Assert.AreEqual(1, animatedProperty.actionGroups.Count,
                $"Expected 1 action group remaining after pruning, but found {animatedProperty.actionGroups.Count}. " +
                "The off-by-one in RemoveRange leaves a redundant dead group.");
        }

        private ReactionRule CreateRuleWithCondition(bool isConstant, bool initiallyActive, bool inverted)
        {
            var targetProp = new TargetProp { TargetObject = null, PropertyName = "test" };
            var rule = new ReactionRule(targetProp, 1.0f);
            rule.Inverted = inverted;

            // Create a condition with ReferenceObject = null so AnalyzeConstants won't modify IsConstant
            var condition = new ControlCondition
            {
                Parameter = "test_param",
                IsConstant = isConstant,
                // Set values so InitiallyActive = true: InitialValue must be in (ParameterValueLo, ParameterValueHi)
                InitialValue = 0.5f,
                ParameterValueLo = 0.0f,
                ParameterValueHi = 1.0f,
                ReferenceObject = null // Prevents AnalyzeConstants from overriding IsConstant
            };

            rule.ControllingConditions.Add(condition);
            return rule;
        }
    }
}
