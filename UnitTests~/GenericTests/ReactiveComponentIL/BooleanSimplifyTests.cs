using System;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class BooleanSimplifyTests : TestBase
    {
        private ReactionGraph CreateGraphWithExpression(IExpression expression)
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(expression, new NullAction()));
            return graph;
        }

        private IExpression SimplifyExpression(IExpression expression)
        {
            var graph = CreateGraphWithExpression(expression);
            BooleanSimplifyTransform.Apply(graph);
            return graph.Nodes[0].Expression;
        }

        [Test]
        public void AndNode_WithFalseConstant_CollapsesToFalse()
        {
            var param = new ParameterExpression("test");
            var expr = new AndNode(param, new Constant(false));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new Constant(false), result);
        }

        [Test]
        public void AndNode_WithTrueConstant_RemovesConstant()
        {
            var param = new ParameterExpression("test");
            var expr = new AndNode(param, new Constant(true));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void AndNode_WithMultipleTrueConstants_RemovesAllConstants()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var expr = new AndNode(new Constant(true), param1, new Constant(true), param2, new Constant(true));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param1, param2), result);
        }

        [Test]
        public void AndNode_EmptyChildren_CollapsesToTrue()
        {
            var expr = new AndNode();

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void AndNode_SingleChild_CollapsesToChild()
        {
            var param = new ParameterExpression("test");
            var expr = new AndNode(param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void AndNode_NestedAnd_Flattens()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            var inner = new AndNode(param2, param3);
            var expr = new AndNode(param1, inner);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param1, param2, param3), result);
        }

        [Test]
        public void AndNode_MultipleNestedAnds_Flattens()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            var param4 = new ParameterExpression("test4");
            var inner1 = new AndNode(param1, param2);
            var inner2 = new AndNode(param3, param4);
            var expr = new AndNode(inner1, inner2);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param1, param2, param3, param4), result);
        }

        [Test]
        public void OrNode_WithTrueConstant_CollapsesToTrue()
        {
            var param = new ParameterExpression("test");
            var expr = new OrNode(param, new Constant(true));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void OrNode_WithFalseConstant_RemovesConstant()
        {
            var param = new ParameterExpression("test");
            var expr = new OrNode(param, new Constant(false));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void OrNode_WithMultipleFalseConstants_RemovesAllConstants()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var expr = new OrNode(new Constant(false), param1, new Constant(false), param2, new Constant(false));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param1, param2), result);
        }

        [Test]
        public void OrNode_EmptyChildren_CollapsesToTrue()
        {
            var expr = new OrNode();

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void OrNode_SingleChild_CollapsesToChild()
        {
            var param = new ParameterExpression("test");
            var expr = new OrNode(param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void OrNode_NestedOr_Flattens()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            var inner = new OrNode(param2, param3);
            var expr = new OrNode(param1, inner);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param1, param2, param3), result);
        }

        [Test]
        public void OrNode_MultipleNestedOrs_Flattens()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            var param4 = new ParameterExpression("test4");
            var inner1 = new OrNode(param1, param2);
            var inner2 = new OrNode(param3, param4);
            var expr = new OrNode(inner1, inner2);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param1, param2, param3, param4), result);
        }

        [Test]
        public void NotNode_WithTrueConstant_CollapsesToFalse()
        {
            var expr = new NotNode(new Constant(true));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new Constant(false), result);
        }

        [Test]
        public void NotNode_WithFalseConstant_CollapsesToTrue()
        {
            var expr = new NotNode(new Constant(false));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void NotNode_DoubleNegation_Collapses()
        {
            var param = new ParameterExpression("test");
            var expr = new NotNode(new NotNode(param));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void NotNode_QuadrupleNegation_Collapses()
        {
            var param = new ParameterExpression("test");
            var expr = new NotNode(new NotNode(new NotNode(new NotNode(param))));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void NotNode_TripleNegation_CollapsesToSingleNot()
        {
            var param = new ParameterExpression("test");
            var expr = new NotNode(new NotNode(new NotNode(param)));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new NotNode(param), result);
        }

        [Test]
        public void ComplexExpression_AndWithOrNested_SimplifiesCorrectly()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            
            // AND(param1, OR(param2, FALSE), TRUE)
            var orNode = new OrNode(param2, new Constant(false));
            var expr = new AndNode(param1, orNode, new Constant(true));

            var result = SimplifyExpression(expr);

            // Should simplify to AND(param1, param2)
            Assert.AreEqual(new AndNode(param1, param2), result);
        }

        [Test]
        public void ComplexExpression_OrWithAndNested_SimplifiesCorrectly()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            
            // OR(param1, AND(param2, TRUE), FALSE)
            var andNode = new AndNode(param2, new Constant(true));
            var expr = new OrNode(param1, andNode, new Constant(false));

            var result = SimplifyExpression(expr);

            // Should simplify to OR(param1, param2)
            Assert.AreEqual(new OrNode(param1, param2), result);
        }

        [Test]
        public void ComplexExpression_DeeplyNested_SimplifiesCompletely()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            
            // NOT(AND(OR(param1, FALSE), NOT(NOT(param2)), TRUE, AND(param3)))
            var orNode = new OrNode(param1, new Constant(false));
            var doubleNot = new NotNode(new NotNode(param2));
            var innerAnd = new AndNode(param3);
            var andNode = new AndNode(orNode, doubleNot, new Constant(true), innerAnd);
            var expr = new NotNode(andNode);

            var result = SimplifyExpression(expr);

            // Should simplify to NOT(AND(param1, param2, param3))
            Assert.AreEqual(new NotNode(new AndNode(param1, param2, param3)), result);
        }

        [Test]
        public void ComplexExpression_WithFalseInAnd_ShortCircuits()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            
            // AND(param1, FALSE, param2)
            var expr = new AndNode(param1, new Constant(false), param2);

            var result = SimplifyExpression(expr);

            // Should collapse to FALSE
            Assert.AreEqual(new Constant(false), result);
        }

        [Test]
        public void ComplexExpression_WithTrueInOr_ShortCircuits()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            
            // OR(param1, TRUE, param2)
            var expr = new OrNode(param1, new Constant(true), param2);

            var result = SimplifyExpression(expr);

            // Should collapse to TRUE
            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void MultipleNodes_SimplifiesEachIndependently()
        {
            var graph = new ReactionGraph();
            
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            
            var expr1 = new AndNode(param1, new Constant(true));
            var expr2 = new OrNode(param2, new Constant(false));
            
            graph.AddNode(new ReactionNode(expr1, new NullAction()));
            graph.AddNode(new ReactionNode(expr2, new NullAction()));
            
            BooleanSimplifyTransform.Apply(graph);

            Assert.AreEqual(param1, graph.Nodes[0].Expression);
            Assert.AreEqual(param2, graph.Nodes[1].Expression);
        }

        [Test]
        public void AndNode_WithOnlyConstants_SimplifiesCorrectly()
        {
            var expr = new AndNode(new Constant(true), new Constant(true), new Constant(true));

            var result = SimplifyExpression(expr);

            // After removing all true constants, should be empty and collapse to TRUE
            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void OrNode_WithOnlyConstants_SimplifiesCorrectly()
        {
            var expr = new OrNode(new Constant(false), new Constant(false), new Constant(false));

            var result = SimplifyExpression(expr);

            // After removing all constants, should be empty and collapse to TRUE
            Assert.AreEqual(new Constant(true), result);
        }

        [Test]
        public void MixedExpression_AndWithNestedOrAndNot_SimplifiesCorrectly()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var param3 = new ParameterExpression("test3");
            
            // AND(param1, NOT(OR(FALSE, param2)), OR(TRUE, param3))
            var innerOr = new OrNode(new Constant(false), param2);
            var notNode = new NotNode(innerOr);
            var outerOr = new OrNode(new Constant(true), param3);
            var expr = new AndNode(param1, notNode, outerOr);

            var result = SimplifyExpression(expr);

            // OR(TRUE, param3) -> TRUE
            // NOT(OR(FALSE, param2)) -> NOT(param2)
            // AND(param1, NOT(param2), TRUE) -> AND(param1, NOT(param2))
            var expected = new AndNode(param1, new NotNode(param2));
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Constant_AloneInExpression_RemainsUnchanged()
        {
            var exprTrue = new Constant(true);
            var exprFalse = new Constant(false);

            var resultTrue = SimplifyExpression(exprTrue);
            var resultFalse = SimplifyExpression(exprFalse);

            Assert.AreEqual(new Constant(true), resultTrue);
            Assert.AreEqual(new Constant(false), resultFalse);
        }

        [Test]
        public void ParameterExpression_AloneInExpression_RemainsUnchanged()
        {
            var param = new ParameterExpression("test", 0.5f, ParameterExpression.ConditionMode.GreaterThan);

            var result = SimplifyExpression(param);

            Assert.AreEqual(param, result);
        }

        // PruneIdenticalConditions tests

        [Test]
        public void AndNode_WithConsecutiveDuplicates_RemovesOneDuplicate()
        {
            var param = new ParameterExpression("test");
            var other = new ParameterExpression("other");
            var expr = new AndNode(param, param, other);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param, other), result);
        }

        [Test]
        public void AndNode_WithAllDuplicates_CollapsesToParam()
        {
            var param = new ParameterExpression("test");
            var expr = new AndNode(param, param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void AndNode_WithThreeConsecutiveDuplicates_CollapsesToParam()
        {
            var param = new ParameterExpression("test");
            var expr = new AndNode(param, param, param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void AndNode_WithMultipleGroupsOfConsecutiveDuplicates_PrunesAllGroups()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            var expr = new AndNode(param1, param1, param2, param2);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param1, param2), result);
        }

        [Test]
        public void AndNode_WithNonConsecutiveDuplicates_Deduped()
        {
            var param = new ParameterExpression("test");
            var other = new ParameterExpression("other");
            var expr = new AndNode(param, other, param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param, other), result);
        }

        [Test]
        public void OrNode_WithConsecutiveDuplicates_RemovesOneDuplicate()
        {
            var param = new ParameterExpression("test");
            var other = new ParameterExpression("other");
            var expr = new OrNode(param, param, other);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param, other), result);
        }

        [Test]
        public void OrNode_WithAllDuplicates_CollapsesToParam()
        {
            var param = new ParameterExpression("test");
            var expr = new OrNode(param, param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(param, result);
        }

        [Test]
        public void OrNode_WithNonConsecutiveDuplicates_Deduped()
        {
            var param = new ParameterExpression("test");
            var other = new ParameterExpression("other");
            var expr = new OrNode(param, other, param);

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param, other), result);
        }

        [Test]
        public void AndNode_ConsecutiveDuplicatesAfterFlattening_Pruned()
        {
            var param = new ParameterExpression("test");
            var other = new ParameterExpression("other");
            // AND(param, AND(param, other)) flattens to AND(param, param, other), then prunes to AND(param, other)
            var expr = new AndNode(param, new AndNode(param, other));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param, other), result);
        }

        [Test]
        public void OrNode_ConsecutiveDuplicatesAfterFlattening_Pruned()
        {
            var param = new ParameterExpression("test");
            var other = new ParameterExpression("other");
            // OR(param, OR(param, other)) flattens to OR(param, param, other), then prunes to OR(param, other)
            var expr = new OrNode(param, new OrNode(param, other));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param, other), result);
        }

        [Test]
        public void AndNode_ConsecutiveDuplicateComplexExpressions_Pruned()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            // AND(OR(a,b), OR(a,b)) — two structurally equal sub-expressions are pruned
            var expr = new AndNode(new OrNode(param1, param2), new OrNode(param1, param2));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new OrNode(param1, param2), result);
        }

        [Test]
        public void OrNode_ConsecutiveDuplicateComplexExpressions_Pruned()
        {
            var param1 = new ParameterExpression("test1");
            var param2 = new ParameterExpression("test2");
            // OR(AND(a,b), AND(a,b)) — two structurally equal sub-expressions are pruned
            var expr = new OrNode(new AndNode(param1, param2), new AndNode(param1, param2));

            var result = SimplifyExpression(expr);

            Assert.AreEqual(new AndNode(param1, param2), result);
        }
    }
}

