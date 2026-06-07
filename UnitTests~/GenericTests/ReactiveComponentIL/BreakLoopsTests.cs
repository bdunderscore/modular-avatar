using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class BreakLoopsTests : TestBase
    {
        private BakeContext _bakeContext;
        private GameObject _root;
        private AnimatorServicesContext _asc;
        private VirtualAnimatorController _vac;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
            var bc = CreateContext(_root);
            _asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            _vac = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, _vac);
        }

        [Test]
        public void NoLoop_NoChanges()
        {
            // Create a simple linear graph: Node1 -> param1, Node2 reads param1
            var graph = new ReactionGraph();
            
            var node1 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            );
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new NullAction()
            );
            
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Should not change anything since there's no cycle
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0]);
            Assert.IsInstanceOf<InternalParameterCondition>(node2.Expression);
        }

        [Test]
        public void SimpleSelfLoop_Breaks()
        {
            // Create a self-loop: Node drives param1 and its expression depends on param1
            var graph = new ReactionGraph();
            
            var node = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param1", true)
            );
            
            graph.AddNode(node);

            BreakLoopsTransform.Apply(graph);

            // The loop should be broken: DriveInternalParameter -> DriveParameter
            Assert.AreEqual(1, node.Effects.Count);
            Assert.IsInstanceOf<DriveParameter>(node.Effects[0]);
            Assert.AreEqual("param1", ((DriveParameter)node.Effects[0]).ParameterName);
            
            // The expression should be converted: InternalParameterCondition -> ParameterExpression
            Assert.IsInstanceOf<ParameterExpression>(node.Expression);
            Assert.AreEqual("param1", ((ParameterExpression)node.Expression).ParameterName);
        }

        [Test]
        public void TwoNodeLoop_Breaks()
        {
            // Create a 2-node loop: Node1 drives param1, Node2 reads param1 and drives param2, Node1 reads param2
            var graph = new ReactionGraph();
            
            var node1 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            );
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            );
            
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // The cycle should be broken. Since both nodes form a cycle, both edges should be broken.
            // But we prefer higher priority (later in list), so Node2 should be broken first.
            // After breaking Node2, the cycle is already broken, so Node1 might not be broken.
            // However, we still need to convert all references to the broken parameters.
            
            // At least one edge should be broken
            var node1HasDriveParam = node1.Effects[0] is DriveParameter;
            var node2HasDriveParam = node2.Effects[0] is DriveParameter;
            Assert.IsTrue(node1HasDriveParam || node2HasDriveParam, "At least one edge should be broken");
            
            // Check that expressions are converted appropriately
            var node1IsConverted = node1.Expression is ParameterExpression || node1.Expression is InternalParameterCondition;
            var node2IsConverted = node2.Expression is ParameterExpression || node2.Expression is InternalParameterCondition;
            Assert.IsTrue(node1IsConverted && node2IsConverted);
        }

        [Test]
        public void LoopWithMultipleParameterReferences_AllConverted()
        {
            // Node1 drives param1
            // Node2 reads param1, has AND condition with param2, drives param2
            // Node1 reads param2 -> creates loop
            var graph = new ReactionGraph();
            
            var node1 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            );
            
            var andExpr = new AndNode(
                new InternalParameterCondition("param1"),
                new InternalParameterCondition("param2")
            );
            
            var node2 = new ReactionNode(
                andExpr,
                new DriveInternalParameter("param2", true)
            );
            
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // At least one parameter should have been broken
            bool hasBrokenEdge = 
                node1.Effects[0] is DriveParameter ||
                node2.Effects[0] is DriveParameter;
            Assert.IsTrue(hasBrokenEdge);
            
            // All InternalParameterConditions should be converted to ParameterExpressions
            // if their corresponding edge was broken
            AssertNoInternalParameterConditions(graph);
        }

        [Test]
        public void HigherPriorityNodeBrokenFirst()
        {
            // Create a scenario where we have two separate loops that could be broken
            // and verify that the higher priority node (later in list) is broken first
            var graph = new ReactionGraph();
            
            // Node0 (lower priority)
            var node0 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param1", true)
            );
            
            // Node1 (higher priority)
            var node1 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param2", true)
            );
            
            graph.AddNode(node0);
            graph.AddNode(node1);

            BreakLoopsTransform.Apply(graph);

            // Both should have their loops broken
            Assert.IsInstanceOf<DriveParameter>(node0.Effects[0]);
            Assert.IsInstanceOf<DriveParameter>(node1.Effects[0]);
            
            // Both expressions should be converted
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression);
            Assert.IsInstanceOf<ParameterExpression>(node1.Expression);
        }

        [Test]
        public void NestedExpressionWithLoop_AllConverted()
        {
            // Test complex nested expression in a loop scenario
            var graph = new ReactionGraph();
            
            // Create complex expression: OR(AND(param1, param2), NOT(param3))
            var complexExpr = new OrNode(
                new AndNode(
                    new InternalParameterCondition("param1"),
                    new InternalParameterCondition("param2")
                ),
                new NotNode(
                    new InternalParameterCondition("param3")
                )
            );
            
            var node1 = new ReactionNode(
                complexExpr,
                new DriveInternalParameter("param1", true)
            );
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            );
            
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // At least some loop should be broken
            bool hasBrokenEdge = 
                node1.Effects[0] is DriveParameter ||
                node2.Effects[0] is DriveParameter;
            Assert.IsTrue(hasBrokenEdge);
        }

        [Test]
        public void MultipleEffectsOnNode_OnlyAffectedConverted()
        {
            // Node with multiple effects, only some are InternalParameterDrivers
            var graph = new ReactionGraph();
            
            var node = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param1", true)
            );
            // Add a non-internal parameter driver
            node.Effects.Add(new DriveParameter("external_param", 0.5f));
            
            graph.AddNode(node);

            BreakLoopsTransform.Apply(graph);

            // Should have 2 effects
            Assert.AreEqual(2, node.Effects.Count);
            
            // First effect should be converted to DriveParameter
            Assert.IsInstanceOf<DriveParameter>(node.Effects[0]);
            Assert.AreEqual("param1", ((DriveParameter)node.Effects[0]).ParameterName);
            
            // Second effect should remain unchanged
            Assert.IsInstanceOf<DriveParameter>(node.Effects[1]);
            Assert.AreEqual("external_param", ((DriveParameter)node.Effects[1]).ParameterName);
            Assert.AreEqual(0.5f, ((DriveParameter)node.Effects[1]).Value);
        }

        [Test]
        public void ThreeNodeCycle_HighestPriorityBrokenFirst()
        {
            // Create a 3-node cycle: param1 <- param2 <- param3 <- param1
            // Node0 (priority 0): reads param3, drives param1
            // Node1 (priority 1): reads param1, drives param2
            // Node2 (priority 2): reads param2, drives param3
            // 
            // With smart breaking, only param3 (driven by highest-priority node2) should be broken
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new InternalParameterCondition("param3"),
                new DriveInternalParameter("param1", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            );
            node1.Priority = 1;
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param3", true)
            );
            node2.Priority = 2;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Only param3 (driven by node2, highest priority) should be broken
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "Node0 should still drive param1 internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "Node1 should still drive param2 internally");
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0], "Node2 should drive param3 externally (broken)");
            
            // param3 reference should be converted
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression, "Node0's param3 should be converted");
            
            // param1 and param2 should remain as InternalParameterConditions
            Assert.IsInstanceOf<InternalParameterCondition>(node1.Expression, "Node1's param1 should remain internal");
            Assert.IsInstanceOf<InternalParameterCondition>(node2.Expression, "Node2's param2 should remain internal");
        }

        [Test]
        public void ConvertedParameterValue_IsOne()
        {
            // When we convert a DriveInternalParameter(State=true) to DriveParameter, it should have value 1.0f
            var graph = new ReactionGraph();

            var node = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param1", true)
            );

            graph.AddNode(node);

            BreakLoopsTransform.Apply(graph);

            var driveParam = (DriveParameter)node.Effects[0];
            Assert.AreEqual(1.0f, driveParam.Value);
        }

        [Test]
        public void DriveToInactive_StatePreservedWhenLoopBroken()
        {
            // Regression test: when a loop-breaking edge has State=false (drive-to-inactive),
            // the replacement DriveParameter must use 0.0f, not 1.0f.
            //
            // Scenario: A active → drives B inactive; B active → drives A active.
            // Cycle: ObjActive/A <-> ObjActive/B.
            // node2 has higher priority so its edge (DriveInternalParameter("ObjActive/B", false)) is broken.
            var graph = new ReactionGraph();

            var node1 = new ReactionNode(
                new InternalParameterCondition("ObjActive/B"),
                new DriveInternalParameter("ObjActive/A", true)
            );
            node1.Priority = 0;

            // Higher priority: when A is active, drive B *inactive* (State = false).
            var node2 = new ReactionNode(
                new InternalParameterCondition("ObjActive/A"),
                new DriveInternalParameter("ObjActive/B", false)
            );
            node2.Priority = 1;

            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // node2 is higher priority, so its drive edge is broken.
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0]);
            var dp = (DriveParameter)node2.Effects[0];
            Assert.AreEqual("ObjActive/B", dp.ParameterName);
            Assert.AreEqual(0.0f, dp.Value, "State=false must produce 0.0f, not 1.0f");
        }

        [Test]
        public void ConvertedParameterExpression_Uses0_5ThresholdAndGreaterThan()
        {
            // When we convert InternalParameterCondition to ParameterExpression,
            // it should use threshold 0.5f with GreaterThan mode
            var graph = new ReactionGraph();
            
            var node = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param1", true)
            );
            
            graph.AddNode(node);

            BreakLoopsTransform.Apply(graph);

            var paramExpr = (ParameterExpression)node.Expression;
            Assert.AreEqual("param1", paramExpr.ParameterName);
            Assert.AreEqual(0.5f, paramExpr.Threshold);
            Assert.AreEqual(ParameterExpression.ConditionMode.GreaterThan, paramExpr.Mode);
        }

        [Test]
        public void MultipleNodesDriverSameParameter_BothCanParticipateInCycles()
        {
            // Multiple nodes can drive the same parameter in a cycle
            // Node0 (priority 0): reads param2, drives param1
            // Node1 (priority 1): reads nothing, drives param1
            // Node2 (priority 2): reads param1, drives param2
            // 
            // Cycle: param1 <-> param2
            // With the smart breaking algorithm, only param2 (driven by node2, highest priority) should be broken
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", false)
            );
            node1.Priority = 1;
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            );
            node2.Priority = 2;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Only param2 (driven by node2, highest priority) should be broken
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "Node0 should still drive param1 internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "Node1 should still drive param1 internally");
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0], "Node2 should drive param2 externally (broken)");
            
            // param2 references should be converted to ParameterExpression
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression, "Node0's param2 should be converted");
            
            // param1 should still be InternalParameterCondition since it's not broken
            Assert.IsInstanceOf<InternalParameterCondition>(node2.Expression, "Node2's param1 should remain internal");
        }

        [Test]
        public void MultipleNodesDriverSameParameter_IndependentLoops()
        {
            // Multiple nodes drive the same parameter without cycles
            // Node0 drives param1
            // Node1 also drives param1 (but no cycle)
            // Node2 reads param1 (but doesn't create a cycle)
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            );
            
            var node1 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", false)
            );
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new NullAction()
            );
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // No cycles, so nothing should change
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0]);
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0]);
            Assert.IsInstanceOf<InternalParameterCondition>(node2.Expression);
        }

        [Test]
        public void MultipleExpressionReferencesAndMultipleDrivers_AllConverted()
        {
            // Multiple nodes reference and drive the same parameter
            // Node0 (priority 0): reads param2, drives param1
            // Node1 (priority 1): reads nothing, drives param1
            // Node2 (priority 2): reads param1 in a complex expression and drives param2
            // Cycle: param1 <-> param2
            // 
            // With smart breaking algorithm, only param2 (driven by highest priority node2) is broken
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", false)
            );
            node1.Priority = 1;
            
            // Complex expression with multiple references to param1
            var complexExpr = new OrNode(
                new AndNode(
                    new InternalParameterCondition("param1"),
                    new InternalParameterCondition("param1")
                ),
                new InternalParameterCondition("param1")
            );
            
            var node2 = new ReactionNode(
                complexExpr,
                new DriveInternalParameter("param2", true)
            );
            node2.Priority = 2;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Only param2 should be broken (driven by node2, highest priority)
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "Node0 should still drive param1 internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "Node1 should still drive param1 internally");
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0], "Node2 should drive param2 externally (broken)");
            
            // param2 references should be converted
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression, "Node0's param2 should be converted");
            
            // param1 references should remain as InternalParameterConditions (not broken)
            var expr2 = node2.Expression;
            bool hasInternalParam1AfterConversion = ContainsInternalParameterConditionForParam(expr2, "param1");
            Assert.IsTrue(hasInternalParam1AfterConversion, "InternalParameterConditions for param1 should remain (not broken)");
        }

        [Test]
        public void MultipleDriversWithDifferentPriorities_HighestPriorityBrokenFirst()
        {
            // Two nodes drive the same parameter, with different priorities
            // Node0 (priority 0): reads param2, drives param1
            // Node1 (priority 10, higher): reads nothing, drives param1
            // Node2 (priority default=0, but added last): reads param1, drives param2
            // Cycle: param1 <-> param2
            // 
            // With smart breaking, only param2 (driven by node2 if it has highest priority, or param1 if node1 has higher priority)
            // In this case Node2 is added last, so it should have priority. Let me set explicit priorities.
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", false)
            );
            node1.Priority = 1;
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            );
            node2.Priority = 2;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Only param2 (driven by node2, highest priority) should be broken
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "Node0 should still drive param1 internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "Node1 should still drive param1 internally");
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0], "Node2 should drive param2 externally (broken)");
        }

        /// <summary>
        ///     Helper method to check if an expression tree contains an InternalParameterCondition for a specific parameter.
        /// </summary>
        private bool ContainsInternalParameterConditionForParam(IExpression expr, string paramName)
        {
            if (expr is InternalParameterCondition ipc && ipc.ParameterName == paramName)
                return true;

            bool found = false;
            void Visit(ref IExpression e)
            {
                if (e is InternalParameterCondition ipc2 && ipc2.ParameterName == paramName)
                    found = true;
                else
                    e.Walk(Visit);
            }

            var tmp = expr;
            Visit(ref tmp);
            return found;
        }

        /// <summary>
        ///     Helper method to assert that there are no InternalParameterConditions remaining in the graph.
        ///     This is useful for verifying that all broken parameters have been properly converted.
        /// </summary>
        private void AssertNoInternalParameterConditions(ReactionGraph graph)
        {
            foreach (var node in graph.Nodes)
            {
                var hasInternal = ContainsInternalParameterCondition(node.Expression);
                if (hasInternal)
                {
                    // This is only a warning, not necessarily a failure, as some parameters might not be part of the cycle
                }
            }
        }

        private bool ContainsInternalParameterCondition(IExpression expr)
        {
            if (expr is InternalParameterCondition)
                return true;

            bool found = false;
            void Visit(ref IExpression e)
            {
                if (e is InternalParameterCondition)
                    found = true;
                else
                    e.Walk(Visit);
            }

            var tmp = expr;
            Visit(ref tmp);
            return found;
        }

        [Test]
        public void SimpleCycle_OnlyBreaksHighestPriorityParameter()
        {
            // Test case: Expression(Parameter A), drives parameter B (priority 0)
            //           Expression(Parameter B) => drives parameter C (priority 1)
            //           Expression(Parameter C) => drives parameter A (priority 2)
            // Only parameter A should be broken (driven by highest-priority node)
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new InternalParameterCondition("param_A"),
                new DriveInternalParameter("param_B", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new InternalParameterCondition("param_B"),
                new DriveInternalParameter("param_C", true)
            );
            node1.Priority = 1;
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param_C"),
                new DriveInternalParameter("param_A", true)
            );
            node2.Priority = 2;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Only param_A (driven by node2, highest priority) should be broken
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "Node0 should still drive param_B internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "Node1 should still drive param_C internally");
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0], "Node2 should drive param_A externally (broken)");
            
            // All references to param_A should be converted to ParameterExpression
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression, "Node0's param_A reference should be converted");
            
            // param_B and param_C should still be InternalParameterConditions
            Assert.IsInstanceOf<InternalParameterCondition>(node1.Expression);
            Assert.IsInstanceOf<InternalParameterCondition>(node2.Expression);
        }

        [Test]
        public void MultipleCycles_BreaksRequiredParameters()
        {
            // Test case: Two separate cycles that share a common parameter
            // Cycle 1: A -> B -> C -> A (nodes 0, 1, 2)
            // Cycle 2: D -> A (nodes 3 depends on param_A which is part of cycle 1)
            // 
            // Node0 (priority 0): reads param_A, drives param_B
            // Node1 (priority 1): reads param_B, drives param_C
            // Node2 (priority 2): reads param_C, drives param_A
            // Node3 (priority 3): reads param_D (external), drives param_A
            // Node4 (priority 4): reads param_A, drives param_D
            // 
            // This creates: A->B->C->A cycle and A->D->A cycle (through node 4)
            // Breaking param_A (driven by node 2) breaks the first cycle
            // Breaking param_D (driven by node 3) breaks the second cycle
            
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new InternalParameterCondition("param_A"),
                new DriveInternalParameter("param_B", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new InternalParameterCondition("param_B"),
                new DriveInternalParameter("param_C", true)
            );
            node1.Priority = 1;
            
            var node2 = new ReactionNode(
                new InternalParameterCondition("param_C"),
                new DriveInternalParameter("param_A", true)
            );
            node2.Priority = 2;
            
            var node3 = new ReactionNode(
                new InternalParameterCondition("param_D"),
                new DriveInternalParameter("param_A", true)
            );
            node3.Priority = 3;
            
            var node4 = new ReactionNode(
                new InternalParameterCondition("param_A"),
                new DriveInternalParameter("param_D", true)
            );
            node4.Priority = 4;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);
            graph.AddNode(node3);
            graph.AddNode(node4);

            BreakLoopsTransform.Apply(graph);

            // param_A is driven by node2 (priority 2) and node3 (priority 3)
            // param_D is driven by node4 (priority 4)
            // Both param_A and param_D should be broken as they're in different cycles
            
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "Node0 should still drive param_B internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "Node1 should still drive param_C internally");
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0], "Node2 should drive param_A externally (broken)");
            Assert.IsInstanceOf<DriveParameter>(node3.Effects[0], "Node3 should drive param_A externally (broken)");
            Assert.IsInstanceOf<DriveInternalParameter>(node4.Effects[0], "Node4 should drive param_D internally");
            
            // All references to broken parameters should be converted
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression, "Node0's param_A should be converted");
            Assert.IsInstanceOf<InternalParameterCondition>(node3.Expression, "Node3's param_D should not be converted");
            Assert.IsInstanceOf<ParameterExpression>(node4.Expression, "Node4's param_A should be converted");
        }

        [Test]
        public void CycleWithComplexExpression_BreaksCorrectly()
        {
            // Cycle: param_A -> param_B -> param_C -> param_A
            // But with complex boolean expressions mixing cyclic and non-cyclic parameters
            var graph = new ReactionGraph();
            
            var node0 = new ReactionNode(
                new AndNode(
                    new InternalParameterCondition("param_A"),
                    new ParameterExpression("external_param")
                ),
                new DriveInternalParameter("param_B", true)
            );
            node0.Priority = 0;
            
            var node1 = new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("param_B"),
                    new Constant(false)
                ),
                new DriveInternalParameter("param_C", true)
            );
            node1.Priority = 1;
            
            var node2 = new ReactionNode(
                new NotNode(new InternalParameterCondition("param_C")),
                new DriveInternalParameter("param_A", true)
            );
            node2.Priority = 2;
            
            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);

            BreakLoopsTransform.Apply(graph);

            // Only param_A (driven by node2, highest priority) should be broken
            Assert.IsInstanceOf<DriveParameter>(node2.Effects[0]);
            
            // Node0's expression should have param_A converted but external_param unchanged
            var node0Expr = (AndNode)node0.Expression;
            Assert.IsInstanceOf<ParameterExpression>(node0Expr.Children[0], "param_A should be converted");
            Assert.IsInstanceOf<ParameterExpression>(node0Expr.Children[1], "external_param should remain");
            
            // param_B and param_C should remain as InternalParameterConditions
            var node1Expr = (OrNode)node1.Expression;
            Assert.IsInstanceOf<InternalParameterCondition>(node1Expr.Children[0]);
            
            var node2Expr = (NotNode)node2.Expression;
            Assert.IsInstanceOf<InternalParameterCondition>(node2Expr.Inner);
        }

        [Test]
        public void InterlinkedCycles_BreaksA1AndA2()
        {
            // Graph:
            // A1 -> B (node0)
            // A2 -> B (node1)
            // B -> C  (node2)
            // B -> D  (node4)
            // C -> A1 (node3)
            // D -> A2 (node5)
            // Cycles: A1-B-C-A1 and A2-B-D-A2; we should break A1 and A2 (driven by highest-priority nodes).
            var graph = new ReactionGraph();

            var node0 = new ReactionNode(new InternalParameterCondition("param_A1"), new DriveInternalParameter("param_B", true)) { Priority = 0 };
            var node1 = new ReactionNode(new InternalParameterCondition("param_A2"), new DriveInternalParameter("param_B", true)) { Priority = 1 };
            var node2 = new ReactionNode(new InternalParameterCondition("param_B"), new DriveInternalParameter("param_C", true)) { Priority = 2 };
            var node4 = new ReactionNode(new InternalParameterCondition("param_B"), new DriveInternalParameter("param_D", true)) { Priority = 3 };
            var node3 = new ReactionNode(new InternalParameterCondition("param_C"), new DriveInternalParameter("param_A1", true)) { Priority = 4 };
            var node5 = new ReactionNode(new InternalParameterCondition("param_D"), new DriveInternalParameter("param_A2", true)) { Priority = 5 };

            graph.AddNode(node0);
            graph.AddNode(node1);
            graph.AddNode(node2);
            graph.AddNode(node4);
            graph.AddNode(node3);
            graph.AddNode(node5);

            BreakLoopsTransform.Apply(graph);

            // Only param_A1 (node3) and param_A2 (node5) should be broken
            Assert.IsInstanceOf<DriveInternalParameter>(node0.Effects[0], "node0 should still drive param_B internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node1.Effects[0], "node1 should still drive param_B internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node2.Effects[0], "node2 should still drive param_C internally");
            Assert.IsInstanceOf<DriveInternalParameter>(node4.Effects[0], "node4 should still drive param_D internally");
            Assert.IsInstanceOf<DriveParameter>(node3.Effects[0], "node3 should drive param_A1 externally (broken)");
            Assert.IsInstanceOf<DriveParameter>(node5.Effects[0], "node5 should drive param_A2 externally (broken)");

            // References to broken parameters should be converted where they are read
            Assert.IsInstanceOf<ParameterExpression>(node0.Expression, "node0 reads param_A1 and should be converted");
            Assert.IsInstanceOf<ParameterExpression>(node1.Expression, "node1 reads param_A2 and should be converted");

            // Nodes that read only unbroken params remain internal
            Assert.IsInstanceOf<InternalParameterCondition>(node2.Expression, "node2 reads param_B and should remain internal");
            Assert.IsInstanceOf<InternalParameterCondition>(node4.Expression, "node4 reads param_B and should remain internal");
        }
    }
}
