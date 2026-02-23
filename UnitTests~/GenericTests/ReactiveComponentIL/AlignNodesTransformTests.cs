using System;
using System.Collections.Generic;
using System.Linq;
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
    public class AlignNodesTransformTests : TestBase
    {
        private GameObject _root;
        private BakeContext _bakeContext;
        private AnimatorServicesContext _asc;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _root = CreateRoot("root");
            var bc = CreateContext(_root);
            _asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var vac = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, vac);
        }

        #region Effect Group Tests

        [Test]
        public void DividesIntoEffectGroups_SingleEffect()
        {
            // A simple graph with one node driving one effect
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].Nodes.Count);
            Assert.AreEqual(new ParameterTarget("param1"), groups[0].TargetKey);
        }

        [Test]
        public void DividesIntoEffectGroups_MultipleEffects()
        {
            // A graph with multiple nodes driving different effects
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(false),
                new DriveInternalParameter("param2", false)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            Assert.AreEqual(2, groups.Count);
            
            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param2")));
            
            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);
            Assert.AreEqual(1, param1Group.Nodes.Count);
            Assert.AreEqual(1, param2Group.Nodes.Count);
        }

        [Test]
        public void DividesIntoEffectGroups_SameEffectMultipleNodes()
        {
            // Multiple nodes driving the same effect
            var graph = new ReactionGraph();
            var node1 = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            );
            var node2 = new ReactionNode(
                new Constant(false),
                new DriveInternalParameter("param1", false)
            );
            graph.AddNode(node1);
            graph.AddNode(node2);

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(2, groups[0].Nodes.Count);
            Assert.AreEqual(new ParameterTarget("param1"), groups[0].TargetKey);
            Assert.That(groups[0].Nodes, Contains.Item(node1));
            Assert.That(groups[0].Nodes, Contains.Item(node2));
        }

        [Test]
        public void LatencyCalculation_TwoOrFewerNodes_LatencyOne()
        {
            // Effect groups with 1 or 2 nodes should have latency 1
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("single", true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("pair1", true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("pair1", false)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var singleGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("single")));
            var pairGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("pair1")));

            Assert.AreEqual(1, singleGroup.Latency);
            Assert.AreEqual(1, pairGroup.Latency);
        }

        [Test]
        public void LatencyCalculation_MoreThanTwoNodes_LatencyTwo()
        {
            // Effect groups with 3+ nodes should have latency 2
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", false)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param1", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(3, groups[0].Nodes.Count);
            Assert.AreEqual(2, groups[0].Latency);
        }

        #endregion

        #region Delay Node Insertion Tests

        [Test]
        public void InsertsDelayNodes_WhenPathLengthsDiffer()
        {
            // Example from the specification:
            // Node A (external effect): OR(InternalParameter B, InternalParameter C)
            // InternalParameter B: DrivesInternalParameter ON when (InternalParameter C OR InternalParameter D)
            // InternalParameter C: Constant
            // InternalParameter D: Constant
            //
            // Expected result:
            // B should be at depth 1
            // C and D should be at depth 2
            // A delay node should be inserted between A and C
            
            var graph = new ReactionGraph();
            
            // Node A: external effect reads B and C
            graph.AddNode(new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("B"),
                    new InternalParameterCondition("C")
                ),
                new DriveInternalParameter("A", true)
            ));
            
            // Node B: reads (C OR D)
            graph.AddNode(new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("C"),
                    new InternalParameterCondition("D")
                ),
                new DriveInternalParameter("B", true)
            ));
            
            // Node C: constant
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("C", true)
            ));
            
            // Node D: constant
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("D", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("A")));
            var bGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("B")));
            var cGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("C")));
            var dGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("D")));
            
            Assert.IsNotNull(aGroup, "Should have A group");
            Assert.IsNotNull(bGroup, "Should have B group");
            Assert.IsNotNull(cGroup, "Should have C group");
            Assert.IsNotNull(dGroup, "Should have D group");
            
            // A has depth 0 (external effect)
            Assert.AreEqual(0, aGroup.Depth, "A should have depth 0");
            
            // B has depth 1 (it's one step back from A)
            Assert.AreEqual(1, bGroup.Depth, "B should have depth 1");
            
            // C and D have depth 2 (they're two steps back from A)
            Assert.AreEqual(2, cGroup.Depth, "C should have depth 2");
            Assert.AreEqual(2, dGroup.Depth, "D should have depth 2");
            
            // Should have delay node for C (with name like "C$d1")
            var cDelayGroup = groups.FirstOrDefault(g => 
                g.TargetKey is ParameterTarget pt && pt.ParameterName.StartsWith("C$d"));
            Assert.IsNotNull(cDelayGroup, "Should have created a delay node for C");
            Assert.AreEqual(1, cDelayGroup.Depth, "C's delay node should be at depth 1");
        }

        [Test]
        public void DelayNodesCreatedForAllMisalignedParameters()
        {
            // Test that delay nodes are created for all parameters that need them
            // A reads B and C
            // B reads D (needs to be delayed for A's consumption)
            // C reads D (reads at same depth as B, no delay needed)
            // D is constant
            
            var graph = new ReactionGraph();
            
            graph.AddNode(new ReactionNode(
                new AndNode(
                    new InternalParameterCondition("B"),
                    new InternalParameterCondition("C")
                ),
                new DriveInternalParameter("A", true)
            ));
            
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("D"),
                new DriveInternalParameter("B", true)
            ));
            
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("D"),
                new DriveInternalParameter("C", true)
            ));
            
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("D", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("A")));
            var bGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("B")));
            var cGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("C")));
            var dGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("D")));
            
            Assert.IsNotNull(aGroup);
            Assert.IsNotNull(bGroup);
            Assert.IsNotNull(cGroup);
            Assert.IsNotNull(dGroup);
            
            // Check that B and C are at same depth (since they have same latency path to A)
            Assert.AreEqual(bGroup.Depth, cGroup.Depth, "B and C should be at same depth");
            
            // Check alignment constraint: all referenced parameters should have correct depth
            foreach (var bRef in GetParameterReferencesFromExpression(bGroup.Nodes[0].Expression))
            {
                var downstreamGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget(bRef)));
                Assert.IsNotNull(downstreamGroup);
                Assert.AreEqual(bGroup.Depth.Value + bGroup.Latency, downstreamGroup.Depth.Value,
                    $"Parameter {bRef} referenced by B should be at depth {bGroup.Depth.Value + bGroup.Latency}");
            }
        }

        [Test]
        public void MultipleDelayNodesForSingleParameter()
        {
            // Test scenario with multiple delay nodes needed:
            // NullAction: OR(A, B, C)
            // IP(A): OR(B, C)
            // IP(B): C
            // C: Constant
            //
            // Expected depths and delays:
            // C: depth 3
            // B: depth 2 (reads C at depth 3, latency 1)
            // A: depth 1 (reads B and C at different depths, latency 1)
            // NullAction: depth 0 (reads A, B (needs delay), C (needs delay), latency 1)
            // Expected delays: B$d1, C$d2
            
            var graph = new ReactionGraph();
            
            // NullAction: OR(A, B, C)
            var nullActionNode = new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("A"),
                    new InternalParameterCondition("B"),
                    new InternalParameterCondition("C")
                ),
                new NullAction("nullAction")
            );
            graph.AddNode(nullActionNode);
            
            // IP(A): OR(B, C)
            graph.AddNode(new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("B"),
                    new InternalParameterCondition("C")
                ),
                new DriveInternalParameter("A", true)
            ));
            
            // IP(B): C
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("C"),
                new DriveInternalParameter("B", true)
            ));
            
            // C: Constant
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("C", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            // Find the original and delay node groups
            var cGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("C")));
            var bGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("B")));
            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("A")));
            
            var bDelay1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("B$d1")));
            var cDelay1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("C$d1")));
            var cDelay2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("C$d2")));
            
            Assert.IsNotNull(cGroup, "Should have C group");
            Assert.IsNotNull(bGroup, "Should have B group");
            Assert.IsNotNull(aGroup, "Should have A group");
            Assert.IsNotNull(bDelay1Group, "Should have B$d1 delay node");
            Assert.IsNotNull(cDelay2Group, "Should have C$d2 delay node");
            
            // Verify delay node structures
            ValidateDelayNode(bDelay1Group, "B$d1", "B");
            ValidateDelayNode(cDelay2Group, "C$d2", "C$d1");
            ValidateDelayNode(cDelay1Group, "C$d1", "C");
            
            // Verify the NullAction references the delayed versions
            var referencesInNullAction = GetParameterReferencesFromExpression(nullActionNode.Expression);
            Assert.That(referencesInNullAction, Does.Not.Contain("B"), 
                "NullAction should not reference original B");
            Assert.That(referencesInNullAction, Does.Not.Contain("C"), 
                "NullAction should not reference original C");
            Assert.That(referencesInNullAction, Contains.Item("A"), 
                "NullAction should reference A (no delay needed)");
            Assert.That(referencesInNullAction, Contains.Item("B$d1"), 
                "NullAction should reference delayed B$d1");
            Assert.That(referencesInNullAction, Contains.Item("C$d2"), 
                "NullAction should reference delayed C$d2");
            
            // Verify A group references are correct
            var referencesInA = GetParameterReferencesFromExpression(aGroup.Nodes[0].Expression);
            Assert.That(referencesInA, Contains.Item("B"), "A should reference B (no delay needed for A)");
            Assert.That(referencesInA, Contains.Item("C$d1"), 
                "A should reference C$d1 (one level of delay from A's perspective)");
        }

        [Test]
        public void DifferingLatenciesRequireSelectiveDelayNodes()
        {
            // Test that delay nodes are only added when the latency difference requires them
            // Three NullAction groups with different latencies reading the same parameter A:
            //
            // NullAction("x"): 3 nodes (latency 2) - highest latency, can reference A directly
            //   Node 1: Constant
            //   Node 2: Constant
            //   Node 3: IP(A)
            //
            // NullAction("y"): 2 nodes (latency 1) - needs delay A$d1 to match x's depth requirement
            //   Node 1: Constant
            //   Node 2: IP(A)
            //
            // NullAction("z"): 1 node (latency 1) - needs delay A$d1 to match x's depth requirement
            //   Node 1: IP(A)
            //
            // IP(A): Constant (depth 2, latency 1)

            var graph = new ReactionGraph();
            
            // NullAction("x") with 3 nodes (latency 2)
            var xNode1 = new ReactionNode(new Constant(true), new NullAction("x"));
            var xNode2 = new ReactionNode(new Constant(true), new NullAction("x"));
            var xNode3 = new ReactionNode(
                new InternalParameterCondition("A"),
                new NullAction("x")
            );
            graph.AddNode(xNode1);
            graph.AddNode(xNode2);
            graph.AddNode(xNode3);
            
            // NullAction("y") with 2 nodes (latency 1)
            var yNode1 = new ReactionNode(new Constant(true), new NullAction("y"));
            var yNode2 = new ReactionNode(
                new InternalParameterCondition("A"),
                new NullAction("y")
            );
            graph.AddNode(yNode1);
            graph.AddNode(yNode2);
            
            // NullAction("z") with 1 node (latency 1)
            var zNode1 = new ReactionNode(
                new InternalParameterCondition("A"),
                new NullAction("z")
            );
            graph.AddNode(zNode1);
            
            // IP(A) driven by constant
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("A", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var xGroup = groups.FirstOrDefault(g => g.TargetKey as string == "x");
            var yGroup = groups.FirstOrDefault(g => g.TargetKey as string == "y");
            var zGroup = groups.FirstOrDefault(g => g.TargetKey as string == "z");
            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("A")));
            
            Assert.IsNotNull(xGroup, "Should have x group");
            Assert.IsNotNull(yGroup, "Should have y group");
            Assert.IsNotNull(zGroup, "Should have z group");
            Assert.IsNotNull(aGroup, "Should have A group");
            
            // Verify latencies
            Assert.AreEqual(2, xGroup.Latency, "x group should have latency 2 (3 nodes)");
            Assert.AreEqual(1, yGroup.Latency, "y group should have latency 1 (2 nodes)");
            Assert.AreEqual(1, zGroup.Latency, "z group should have latency 1 (1 node)");
            
            // All external effects have depth 0
            Assert.AreEqual(0, xGroup.Depth, "x should have depth 0 (external effect)");
            Assert.AreEqual(0, yGroup.Depth, "y should have depth 0 (external effect)");
            Assert.AreEqual(0, zGroup.Depth, "z should have depth 0 (external effect)");
            Assert.AreEqual(2, aGroup.Depth, "A should have depth 2");
            
            // x requires A at depth 0+2=2, A is at depth 0, so needs delay A$d2
            // y and z require A at depth 0+1=1, A is at depth 0, so need delay A$d1
            var aDelay1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("A$d1")));
            var aDelay2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("A$d2")));
            
            Assert.IsNotNull(aDelay1Group, "Should have delay node A$d1 for y and z");
            
            // Verify x references A$d2 (the furthest delayed version)
            var xReferences = GetParameterReferencesFromExpression(xNode3.Expression);
            Assert.That(xReferences, Does.Contain("A"), "x should reference original A");
            
            // Verify y and z reference A$d1
            var yReferences = GetParameterReferencesFromExpression(yNode2.Expression);
            Assert.That(yReferences, Does.Not.Contain("A"), 
                "y should not reference original A");
            Assert.That(yReferences, Contains.Item("A$d1"), 
                "y should reference A$d1");
            
            var zReferences = GetParameterReferencesFromExpression(zNode1.Expression);
            Assert.That(zReferences, Does.Not.Contain("A"), 
                "z should not reference original A");
            Assert.That(zReferences, Contains.Item("A$d1"), 
                "z should reference A$d1");
        }

        [Test]
        public void NoDelayNodesWhenPathLengthsSame()
        {
            // Create a graph where all paths from input to output have the same length
            var graph = new ReactionGraph();
            
            // Parameter input (no internal params)
            graph.AddNode(new ReactionNode(
                new ParameterExpression("externalParam"),
                new DriveInternalParameter("param1", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            // Should have only the original node groups, no delay nodes
            Assert.AreEqual(1, groups.Count);
            var param1Group = groups[0];
            Assert.AreEqual(new ParameterTarget("param1"), param1Group.TargetKey);
            Assert.AreEqual(1, param1Group.Nodes.Count);
        }

        [Test]
        public void DelayNodeStructure_HasOnAndOffNodes()
        {
            // Delay nodes should consist of exactly 2 nodes: one for true, one for false
            var graph = new ReactionGraph();
            
            graph.AddNode(new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("B"),
                    new InternalParameterCondition("C")
                ),
                new DriveInternalParameter("A", true)
            ));
            
            graph.AddNode(new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("C"),
                    new InternalParameterCondition("D")
                ),
                new DriveInternalParameter("B", true)
            ));
            
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("C", true)
            ));
            
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("D", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            // Find delay nodes
            var delayGroups = groups.Where(g => 
                g.TargetKey is ParameterTarget pt && pt.ParameterName.Contains("$d")
            ).ToList();
            
            Assert.Greater(delayGroups.Count, 0, "Should have created at least one delay node");
            
            foreach (var delayGroup in delayGroups)
            {
                // Extract the original parameter name from delay node name (e.g., "C$d1" -> "C")
                var delayParamName = ((ParameterTarget)delayGroup.TargetKey).ParameterName;
                var originalParamName = delayParamName.Substring(0, delayParamName.IndexOf("$d"));
                ValidateDelayNode(delayGroup, delayParamName, originalParamName);
            }
        }

        #endregion

        #region Depth Assignment Tests

        [Test]
        public void ExternalEffectsHaveDepthZero()
        {
            // External effects (non-internal parameter targets) should get depth 0
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("internalParam", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("internalParam")));
            Assert.IsNotNull(group);
            Assert.AreEqual(0, group.Depth);
        }

        [Test]
        public void InternalParameterDepthsIncrement()
        {
            // Internal parameters should have increasing depths as we go backwards
            var graph = new ReactionGraph();
            
            // param1 is driven by param2 (internal)
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            ));
            
            // param2 has no further inputs
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param2", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param2")));
            
            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);
            Assert.AreEqual(0, param1Group.Depth, "External effect should have depth 0");
            Assert.Greater(param2Group.Depth, param1Group.Depth, "Upstream parameter should have greater depth");
        }

        [Test]
        public void DepthAssignmentConsidersLatency()
        {
            // Depth assignment should account for effect group latency
            var graph = new ReactionGraph();
            
            // Node driving param1 with 2 internal nodes (latency 1)
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            ));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", false)
            ));
            
            // Node driving param2 with 3 internal nodes (latency 2)
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param2", true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param2", false)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param2", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param2")));
            
            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);
            
            // param1 should have depth 0, latency 1
            // param2 should have depth >= 1 (latency of param1 + depth of param1 = 1 + 0 = 1)
            Assert.AreEqual(0, param1Group.Depth);
            Assert.AreEqual(1, param1Group.Latency);
            Assert.GreaterOrEqual(param2Group.Depth, 1);
            Assert.AreEqual(2, param2Group.Latency);
        }

        #endregion

        #region Complex Chain Tests

        [Test]
        public void HandlesChainOfInternalParameters()
        {
            // Create a chain: input -> param1 -> param2 -> param3 -> output
            var graph = new ReactionGraph();
            
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param3"),
                new DriveInternalParameter("output", true)
            ));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param3", true)
            ));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            ));
            graph.AddNode(new ReactionNode(
                new ParameterExpression("input"),
                new DriveInternalParameter("param1", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            // Verify we got the correct groups
            Assert.That(groups.Any(g => g.TargetKey.Equals(new ParameterTarget("output"))));
            Assert.That(groups.Any(g => g.TargetKey.Equals(new ParameterTarget("param1"))));
            Assert.That(groups.Any(g => g.TargetKey.Equals(new ParameterTarget("param2"))));
            Assert.That(groups.Any(g => g.TargetKey.Equals(new ParameterTarget("param3"))));
            
            // All groups should have valid depths
            foreach (var group in groups)
            {
                Assert.IsTrue(group.Depth.HasValue, $"Group {group.TargetKey} should have a depth assigned");
            }
        }

        [Test]
        public void HandlesBranchingPaths()
        {
            // Create a graph with branching:
            //         input
            //        /     \
            //    param1   param2
            //        \     /
            //        output
            
            var graph = new ReactionGraph();
            
            // output depends on both param1 and param2
            graph.AddNode(new ReactionNode(
                new AndNode(
                    new InternalParameterCondition("param1"),
                    new InternalParameterCondition("param2")
                ),
                new DriveInternalParameter("output", true)
            ));
            
            // param1 driven by input
            graph.AddNode(new ReactionNode(
                new ParameterExpression("input"),
                new DriveInternalParameter("param1", true)
            ));
            
            // param2 also driven by input
            graph.AddNode(new ReactionNode(
                new ParameterExpression("input"),
                new DriveInternalParameter("param2", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var outputGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("output")));
            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new ParameterTarget("param2")));
            
            Assert.IsNotNull(outputGroup);
            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);
            
            // Both param1 and param2 should have the same depth (since they have the same latency to output)
            Assert.AreEqual(param1Group.Depth, param2Group.Depth);
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void EmptyGraph_NoError()
        {
            var graph = new ReactionGraph();

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            Assert.AreEqual(0, groups.Count);
        }

        [Test]
        public void SingleNodeNoInternalParameters_NoError()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ParameterExpression("externalParam"),
                new DriveInternalParameter("output", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(0, groups[0].Depth);
        }

        [Test]
        public void CycleThrowsException()
        {
            // Create a cycle: param1 reads param2, param2 reads param1
            var graph = new ReactionGraph();
            
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            ));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param2", true)
            ));

            // Should throw an exception due to invalid edge depth
            Assert.Throws<Exception>(() => AlignNodesTransform.Apply(_bakeContext, graph));
        }

        [Test]
        public void SelfLoopThrowsException()
        {
            // Create a self-loop: param1 reads itself
            var graph = new ReactionGraph();
            
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param1"),
                new DriveInternalParameter("param1", true)
            ));

            // Should throw an exception due to invalid edge depth
            Assert.Throws<Exception>(() => AlignNodesTransform.Apply(_bakeContext, graph));
        }

        #endregion

        #region Expression Replacement Tests

        [Test]
        public void ReplacesInternalParameterReferences_WithDelayedVersion()
        {
            // Scenario:
            // Group 1: NullAction("a"): OR(IP(B), IP(C))
            // Group 2: DriveInternalParameter(B): IP(C)
            // Group 3: IP(C): Constant
            //
            // B reads C (path length 1)
            // "a" reads both B and C (B at depth 1, C at depth 2)
            // C needs delay node when read by "a" since C is at depth 2
            // Expected: delay node C$d1 should be created, and NullAction should reference C$d1, not C
            
            var graph = new ReactionGraph();
            
            // Group 1: NullAction("a"): OR(IP(B), IP(C))
            var nullActionNode = new ReactionNode(
                new OrNode(
                    new InternalParameterCondition("B"),
                    new InternalParameterCondition("C")
                ),
                new NullAction("a")
            );
            graph.AddNode(nullActionNode);
            
            // Group 2: DriveInternalParameter(B): IP(C)
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("C"),
                new DriveInternalParameter("B", true)
            ));
            
            // Group 3: IP(C): Constant
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("C", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            // Verify a delay node was created for C
            var cDelayGroup = groups.FirstOrDefault(g => 
                g.TargetKey is ParameterTarget pt && pt.ParameterName == "C$d1");
            Assert.IsNotNull(cDelayGroup, "Should have created a delay node C$d1");
            
            // Validate the delay node structure
            ValidateDelayNode(cDelayGroup, "C$d1", "C");

            // Verify the NullAction node now references C$d1 instead of C
            var referencesInNullAction = GetParameterReferencesFromExpression(nullActionNode.Expression);
            Assert.That(referencesInNullAction, Does.Not.Contain("C"), 
                "NullAction should not reference original C");
            Assert.That(referencesInNullAction, Contains.Item("C$d1"), 
                "NullAction should reference the delay node C$d1");

            // Verify all referenced parameters exist in the output groups
            foreach (var group in groups)
            {
                foreach (var node in group.Nodes)
                {
                    var referencedParams = GetParameterReferencesFromExpression(node.Expression);
                    foreach (var param in referencedParams)
                    {
                        var paramTarget = new ParameterTarget(param);
                        Assert.That(groups.Any(g => g.TargetKey.Equals(paramTarget)),
                            $"Referenced parameter {param} should exist in output groups");
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private HashSet<string> GetParameterReferencesFromExpression(IExpression expr)
        {
            var references = new HashSet<string>();
            Visit(ref expr);
            return references;

            void Visit(ref IExpression e)
            {
                if (e is InternalParameterCondition condition)
                {
                    references.Add(condition.ParameterName);
                }
                else
                {
                    e.Walk(Visit);
                }
            }
        }

        /// <summary>
        /// Validates that a delay node has the correct structure.
        /// A delay node should have exactly 2 nodes (on/off) that:
        /// - onFalse: Constant(true) → DriveInternalParameter(delayParamName, false)
        /// - onTrue: InternalParameterCondition(originalParamName) → DriveInternalParameter(delayParamName, true)
        /// </summary>
        private void ValidateDelayNode(EffectGroup delayGroup, string delayParamName, string originalParamName)
        {
            // Validate basic structure
            Assert.AreEqual(2, delayGroup.Nodes.Count, 
                $"Delay group {delayParamName} should have exactly 2 nodes (on/off)");
            Assert.AreEqual(1, delayGroup.Latency, 
                $"Delay node {delayParamName} should have latency 1");
            Assert.AreEqual(delayParamName, ((ParameterTarget)delayGroup.TargetKey).ParameterName,
                "Delay group target key should match delay parameter name");
            
            // Find the onFalse and onTrue nodes
            var onFalseNode = delayGroup.Nodes.FirstOrDefault(n => 
                n.Expression is Constant c && c.Value == true);
            var onTrueNode = delayGroup.Nodes.FirstOrDefault(n => 
                n.Expression is InternalParameterCondition);
            
            Assert.IsNotNull(onFalseNode, 
                $"Delay node {delayParamName} should have an onFalse node (Constant(true))");
            Assert.IsNotNull(onTrueNode, 
                $"Delay node {delayParamName} should have an onTrue node (InternalParameterCondition)");
            
            // Validate onFalse node: Constant(true) → DriveInternalParameter(delayParamName, false)
            Assert.AreEqual(1, onFalseNode.Effects.Count);
            var onFalseEffect = onFalseNode.Effects[0] as DriveInternalParameter;
            Assert.IsNotNull(onFalseEffect, 
                $"onFalse node effect in {delayParamName} should be DriveInternalParameter");
            Assert.AreEqual(delayParamName, onFalseEffect.ParameterName,
                $"onFalse node should drive {delayParamName}");
            Assert.AreEqual(false, onFalseEffect.State, 
                $"onFalse node in {delayParamName} should drive false");
            
            // Validate onTrue node: InternalParameterCondition(originalParamName) → DriveInternalParameter(delayParamName, true)
            Assert.AreEqual(1, onTrueNode.Effects.Count);
            var onTrueEffect = onTrueNode.Effects[0] as DriveInternalParameter;
            Assert.IsNotNull(onTrueEffect, 
                $"onTrue node effect in {delayParamName} should be DriveInternalParameter");
            Assert.AreEqual(delayParamName, onTrueEffect.ParameterName,
                $"onTrue node should drive {delayParamName}");
            Assert.AreEqual(true, onTrueEffect.State, 
                $"onTrue node in {delayParamName} should drive true");
            
            var onTrueCondition = onTrueNode.Expression as InternalParameterCondition;
            Assert.IsNotNull(onTrueCondition, 
                $"onTrue node expression in {delayParamName} should be InternalParameterCondition");
            Assert.AreEqual(originalParamName, onTrueCondition.ParameterName, 
                $"onTrue node should condition on {originalParamName}");
        }

        #endregion
    }
}






