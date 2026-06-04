using System;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using InternalParameterTarget = nadena.dev.modular_avatar.core.editor.rc.Graph.InternalParameterTarget;
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
            Assert.AreEqual(new InternalParameterTarget("param1"), groups[0].TargetKey);
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
            
            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param2")));
            
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
            Assert.AreEqual(new InternalParameterTarget("param1"), groups[0].TargetKey);
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

            var singleGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("single")));
            var pairGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("pair1")));

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

            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("A")));
            var bGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("B")));
            var cGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("C")));
            var dGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("D")));
            
            Assert.IsNotNull(aGroup, "Should have A group");
            Assert.IsNotNull(bGroup, "Should have B group");
            Assert.IsNotNull(cGroup, "Should have C group");
            Assert.IsNotNull(dGroup, "Should have D group");
            
            ValidateDelayForward(AlignNodesTransform.DelayParamName("C", 1), "C");
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

            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("A")));
            var bGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("B")));
            var cGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("C")));
            var dGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("D")));
            
            Assert.IsNotNull(aGroup);
            Assert.IsNotNull(bGroup);
            Assert.IsNotNull(cGroup);
            Assert.IsNotNull(dGroup);
            
            // Check alignment: all parameters referenced by B must exist as groups or be delay params
            foreach (var bRef in GetParameterReferencesFromExpression(bGroup.Nodes[0].Expression))
            {
                var paramTarget = new InternalParameterTarget(bRef);
                Assert.That(groups.Any(g => g.TargetKey.Equals(paramTarget)) || AlignNodesTransform.IsDelayParam(bRef),
                    $"Parameter {bRef} referenced by B should exist as a group or be a delay param");
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
            var cGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("C")));
            var bGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("B")));
            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("A")));
            
            Assert.IsNotNull(cGroup, "Should have C group");
            Assert.IsNotNull(bGroup, "Should have B group");
            Assert.IsNotNull(aGroup, "Should have A group");

            // Verify delay forwarding structures
            ValidateDelayForward(AlignNodesTransform.DelayParamName("B", 1), "B");
            ValidateDelayForward(AlignNodesTransform.DelayParamName("C", 2), AlignNodesTransform.DelayParamName("C", 1));
            ValidateDelayForward(AlignNodesTransform.DelayParamName("C", 1), "C");
            
            // Verify the NullAction references the delayed versions
            var referencesInNullAction = GetParameterReferencesFromExpression(nullActionNode.Expression);
            Assert.That(referencesInNullAction, Does.Not.Contain("B"),
                "NullAction should not reference original B");
            Assert.That(referencesInNullAction, Does.Not.Contain("C"),
                "NullAction should not reference original C");
            Assert.That(referencesInNullAction, Contains.Item("A"),
                "NullAction should reference A (no delay needed)");
            Assert.That(referencesInNullAction, Contains.Item(AlignNodesTransform.DelayParamName("B", 1)),
                "NullAction should reference delayed B");
            Assert.That(referencesInNullAction, Contains.Item(AlignNodesTransform.DelayParamName("C", 2)),
                "NullAction should reference delayed C");

            // Verify A group references are correct
            var referencesInA = GetParameterReferencesFromExpression(aGroup.Nodes[0].Expression);
            Assert.That(referencesInA, Contains.Item("B"), "A should reference B (no delay needed for A)");
            Assert.That(referencesInA, Contains.Item(AlignNodesTransform.DelayParamName("C", 1)),
                "A should reference C (one level of delay from A's perspective)");
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
            var aGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("A")));
            
            Assert.IsNotNull(xGroup, "Should have x group");
            Assert.IsNotNull(yGroup, "Should have y group");
            Assert.IsNotNull(zGroup, "Should have z group");
            Assert.IsNotNull(aGroup, "Should have A group");
            
            // Verify latencies
            Assert.AreEqual(2, xGroup.Latency, "x group should have latency 2 (3 nodes)");
            Assert.AreEqual(1, yGroup.Latency, "y group should have latency 1 (2 nodes)");
            Assert.AreEqual(1, zGroup.Latency, "z group should have latency 1 (1 node)");
            
            // x (latency 2) reads A directly; y and z (latency 1) need A delayed by 1
            ValidateDelayForward(AlignNodesTransform.DelayParamName("A", 1), "A");
            
            // Verify x references A$d2 (the furthest delayed version)
            var xReferences = GetParameterReferencesFromExpression(xNode3.Expression);
            Assert.That(xReferences, Does.Contain("A"), "x should reference original A");
            
            // Verify y and z reference A$d1
            var yReferences = GetParameterReferencesFromExpression(yNode2.Expression);
            Assert.That(yReferences, Does.Not.Contain("A"),
                "y should not reference original A");
            Assert.That(yReferences, Contains.Item(AlignNodesTransform.DelayParamName("A", 1)),
                "y should reference delayed A");

            var zReferences = GetParameterReferencesFromExpression(zNode1.Expression);
            Assert.That(zReferences, Does.Not.Contain("A"),
                "z should not reference original A");
            Assert.That(zReferences, Contains.Item(AlignNodesTransform.DelayParamName("A", 1)),
                "z should reference delayed A");
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
            Assert.AreEqual(new InternalParameterTarget("param1"), param1Group.TargetKey);
            Assert.AreEqual(1, param1Group.Nodes.Count);
        }

        [Test]
        public void AndGateGraph_InsertsDelayNodesForLatencyOneGroups()
        {
            // Bug repro: ParameterExpression references to internal parameters should be tracked
            // as dependency edges, just like InternalParameterCondition references.
            //
            // A_OFF/B_OFF/C_OFF are internal parameters driven by Constant nodes.
            // All consumer groups check them via ParameterExpression (float range checks).
            // AND has 3 nodes (latency=2); NOT_A/A_OBJ/B_OBJ/C_OBJ have 1 node (latency=1).
            //
            // Expected: A_OFF depth=2, delay nodes A_OFF$d1/B_OFF$d1/C_OFF$d1 at depth=1,
            // and the latency-1 nodes' ParameterExpression names rewritten to $d1 versions.
            // Currently fails because AlignNodesTransform only tracks InternalParameterCondition.

            var graph = new ReactionGraph();

            // AND group: 3 nodes → latency=2
            var andNode0 = new ReactionNode(new ParameterExpression("A_OFF"), new NullAction("AND"));
            var andNode1 = new ReactionNode(new ParameterExpression("B_OFF"), new NullAction("AND"));
            var andNode2 = new ReactionNode(new ParameterExpression("C_OFF"), new NullAction("AND"));
            graph.AddNode(andNode0);
            graph.AddNode(andNode1);
            graph.AddNode(andNode2);

            // Latency-1 external-effect groups: 1 node each
            var notANode = new ReactionNode(new ParameterExpression("A_OFF"), new NullAction("NOT_A"));
            var aObjNode = new ReactionNode(new ParameterExpression("A_OFF"), new NullAction("A_OBJ"));
            var bObjNode = new ReactionNode(new ParameterExpression("B_OFF"), new NullAction("B_OBJ"));
            var cObjNode = new ReactionNode(new ParameterExpression("C_OFF"), new NullAction("C_OBJ"));
            graph.AddNode(notANode);
            graph.AddNode(aObjNode);
            graph.AddNode(bObjNode);
            graph.AddNode(cObjNode);

            var byEffect = AlignNodesTransform.CreateEffectGroups(_bakeContext, graph);
            var aligned = AlignNodesTransform.Apply(_bakeContext, byEffect);

            // Locate groups
            var andGroup  = aligned.FirstOrDefault(g => g.TargetKey as string == "AND");
            var notAGroup = aligned.FirstOrDefault(g => g.TargetKey as string == "NOT_A");
            var aObjGroup = aligned.FirstOrDefault(g => g.TargetKey as string == "A_OBJ");
            var bObjGroup = aligned.FirstOrDefault(g => g.TargetKey as string == "B_OBJ");
            var cObjGroup = aligned.FirstOrDefault(g => g.TargetKey as string == "C_OBJ");

            Assert.IsNotNull(andGroup,  "AND group should exist");
            Assert.IsNotNull(notAGroup, "NOT_A group should exist");
            Assert.IsNotNull(aObjGroup, "A_OBJ group should exist");
            Assert.IsNotNull(bObjGroup, "B_OBJ group should exist");
            Assert.IsNotNull(cObjGroup, "C_OBJ group should exist");

            // Latencies
            Assert.AreEqual(2, andGroup.Latency,  "AND has 3 nodes → latency 2");
            Assert.AreEqual(1, notAGroup.Latency, "NOT_A has 1 node → latency 1");
            Assert.AreEqual(1, aObjGroup.Latency, "A_OBJ has 1 node → latency 1");
            Assert.AreEqual(1, bObjGroup.Latency, "B_OBJ has 1 node → latency 1");
            Assert.AreEqual(1, cObjGroup.Latency, "C_OBJ has 1 node → latency 1");

            ValidateDelayForward(AlignNodesTransform.DelayParamName("A_OFF", 1), "A_OFF");
            ValidateDelayForward(AlignNodesTransform.DelayParamName("B_OFF", 1), "B_OFF");
            ValidateDelayForward(AlignNodesTransform.DelayParamName("C_OFF", 1), "C_OFF");

            // AND nodes keep original param names (latency-2 consumer, no rewrite needed)
            Assert.AreEqual("A_OFF", ((ParameterExpression)andNode0.Expression).ParameterName,
                "AND node0 should still reference A_OFF");
            Assert.AreEqual("B_OFF", ((ParameterExpression)andNode1.Expression).ParameterName,
                "AND node1 should still reference B_OFF");
            Assert.AreEqual("C_OFF", ((ParameterExpression)andNode2.Expression).ParameterName,
                "AND node2 should still reference C_OFF");

            // Latency-1 nodes should be rewritten to reference delay params
            Assert.AreEqual(AlignNodesTransform.DelayParamName("A_OFF", 1), ((ParameterExpression)notANode.Expression).ParameterName,
                "NOT_A should reference delayed A_OFF");
            Assert.AreEqual(AlignNodesTransform.DelayParamName("A_OFF", 1), ((ParameterExpression)aObjNode.Expression).ParameterName,
                "A_OBJ should reference delayed A_OFF");
            Assert.AreEqual(AlignNodesTransform.DelayParamName("B_OFF", 1), ((ParameterExpression)bObjNode.Expression).ParameterName,
                "B_OBJ should reference delayed B_OFF");
            Assert.AreEqual(AlignNodesTransform.DelayParamName("C_OFF", 1), ((ParameterExpression)cObjNode.Expression).ParameterName,
                "C_OBJ should reference delayed C_OFF");
        }

        [Test]
        public void DelayNodeStructure_HasForwardingEntry()
        {
            // Delay params are forwarded via direct blend tree children, not via EffectGroups.
            // Each delay level D[i] is a clip writing constant 1 to the delay param, with
            // DirectBlendParameter = the prior level (or the original param for level 1).
            var graph = new ReactionGraph();

            // External consumer of A so the graph has a path to ExternalEffect
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("A"),
                new NullAction("output")
            ));

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

            AlignNodesTransform.Apply(_bakeContext, graph);

            // C is 2 hops from the external effect via A; when read by A (1 hop away),
            // C must be delayed by 1 frame. Verify the blend tree forwarding entry was created.
            Assert.IsNotEmpty(_bakeContext.RootTree.Children, "Should have created at least one delay forwarding entry");
            ValidateDelayForward(AlignNodesTransform.DelayParamName("C", 1), "C");
        }

        #endregion

        #region Depth Assignment Tests

        [Test]
        public void ConstantDrivenParameter_NoDelayNodesCreated()
        {
            // A parameter driven only by constants needs no delay alignment
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("internalParam", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("internalParam")));
            Assert.IsNotNull(group);
            Assert.That(groups.All(g => !AlignNodesTransform.IsDelayParam(g.TargetKey.ToString())),
                "No delay nodes should be created for a constant-driven parameter");
        }

        [Test]
        public void LinearChain_NoDelayNodesNeeded()
        {
            // A simple linear chain (param1 reads param2) needs no delay nodes
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("param2"),
                new DriveInternalParameter("param1", true)
            ));

            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveInternalParameter("param2", true)
            ));

            var groups = AlignNodesTransform.Apply(_bakeContext, graph);

            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param2")));

            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);
            Assert.That(groups.All(g => !AlignNodesTransform.IsDelayParam(g.TargetKey.ToString())),
                "No delay nodes should be needed in a simple linear chain");
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

            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param2")));
            
            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);

            // param1 has latency 1 (2 nodes), param2 has latency 2 (3 nodes)
            Assert.AreEqual(1, param1Group.Latency);
            Assert.AreEqual(2, param2Group.Latency);
            // No delay nodes needed: param2 is already at the right depth for param1 to read it
            Assert.That(groups.All(g => !AlignNodesTransform.IsDelayParam(g.TargetKey.ToString())),
                "No delay nodes should be needed when latency accounts for the depth difference");
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
            Assert.That(groups.Any(g => g.TargetKey.Equals(new InternalParameterTarget("output"))));
            Assert.That(groups.Any(g => g.TargetKey.Equals(new InternalParameterTarget("param1"))));
            Assert.That(groups.Any(g => g.TargetKey.Equals(new InternalParameterTarget("param2"))));
            Assert.That(groups.Any(g => g.TargetKey.Equals(new InternalParameterTarget("param3"))));
            
            // All expected groups are present (verified above)
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

            var outputGroup = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("output")));
            var param1Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param1")));
            var param2Group = groups.FirstOrDefault(g => g.TargetKey.Equals(new InternalParameterTarget("param2")));
            
            Assert.IsNotNull(outputGroup);
            Assert.IsNotNull(param1Group);
            Assert.IsNotNull(param2Group);
            
            // Both param1 and param2 have the same latency path to output, so no delay nodes needed
            Assert.That(groups.All(g => !AlignNodesTransform.IsDelayParam(g.TargetKey.ToString())),
                "No delay nodes should be needed when both inputs have equal latency paths");
        }

        #endregion

        #region EffectGroup Emit Tests

        [Test]
        public void Emit_InternalParameterCondition_TrueCaseUsesOnGreaterEquals()
        {
            // Regression test: EmitCondition used to call new BranchNode(name, onTrue, onFalse)
            // but the BranchNode constructor signature is (name, onLess, onGreaterEquals),
            // so the true and false branches were swapped for InternalParameterCondition.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("p"),
                new DriveInternalParameter("effect", true)
            ));

            var groups = AlignNodesTransform.CreateEffectGroups(_bakeContext, graph);
            var eg = groups[new InternalParameterTarget("effect")];

            var rootNode = eg.Emit();

            var branchNode = ((ProxyNode)rootNode).Target as BranchNode;
            Assert.IsNotNull(branchNode, "EmitCondition should produce a BranchNode for InternalParameterCondition");
            Assert.AreEqual("p", branchNode.Parameter);

            // When p >= threshold (true), the effect MotionNode must play
            var trueTarget = ((ProxyNode)branchNode.OnGreaterEquals).Target;
            Assert.IsInstanceOf<MotionNode>(trueTarget,
                "OnGreaterEquals (p=true) must resolve to the effect MotionNode, not the empty node");

            // When p < threshold (false), no effect should play
            var falseTarget = ((ProxyNode)branchNode.OnLessThan).Target;
            Assert.IsNotInstanceOf<MotionNode>(falseTarget,
                "OnLessThan (p=false) must not resolve to the effect MotionNode");
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

            // Validate the delay forwarding structure
            ValidateDelayForward(AlignNodesTransform.DelayParamName("C", 1), "C");

            // Verify the NullAction node now references the delay node instead of C
            var referencesInNullAction = GetParameterReferencesFromExpression(nullActionNode.Expression);
            Assert.That(referencesInNullAction, Does.Not.Contain("C"),
                "NullAction should not reference original C");
            Assert.That(referencesInNullAction, Contains.Item(AlignNodesTransform.DelayParamName("C", 1)),
                "NullAction should reference the delay node for C");

            // Verify all referenced parameters exist in the output groups or are delay params
            foreach (var group in groups)
            {
                foreach (var node in group.Nodes)
                {
                    var referencedParams = GetParameterReferencesFromExpression(node.Expression);
                    foreach (var param in referencedParams)
                    {
                        var paramTarget = new InternalParameterTarget(param);
                        Assert.That(groups.Any(g => g.TargetKey.Equals(paramTarget)) || AlignNodesTransform.IsDelayParam(param),
                            $"Referenced parameter {param} should exist in output groups or be a delay param");
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
        /// Validates that a delay parameter forwarding entry exists in the root blend tree.
        /// GetDelayedParameter creates a direct blend tree child where:
        ///   DirectBlendParameter = sourceParamName  (copies its value as the blend weight)
        ///   Motion = a VirtualClip writing constant 1 to delayParamName
        /// This results in delayParamName tracking sourceParamName with one frame of latency.
        /// </summary>
        private void ValidateDelayForward(string delayParamName, string sourceParamName)
        {
            var forwardingClip = _bakeContext.RootTree.Children
                .Where(c => c.DirectBlendParameter == sourceParamName)
                .Select(c => c.Motion as VirtualClip)
                .FirstOrDefault(clip => clip != null &&
                                 clip.GetFloatCurve("", typeof(Animator), delayParamName) != null);

            Assert.IsNotNull(forwardingClip,
                $"RootTree should have a forwarding entry: {sourceParamName} → {delayParamName}");
        }

        #endregion
    }
}






