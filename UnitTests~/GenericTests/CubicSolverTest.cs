using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;

namespace modular_avatar_tests
{
    public class CubicSolverTest
    {
        private const double RootTolerance = 1e-6;

        [Test]
        public void FindsThreeDistinctRootsInInterval()
        {
            // (x - 0.2)(x - 0.5)(x - 0.8)
            // = (x^2 - 0.7x + 0.1)(x - 0.8)
            // = x^3 - 1.5x^2 + 0.66x - 0.08.
            const double a = 1.0;
            const double b = -1.5;
            const double c = 0.66;
            const double d = -0.08;
            var expected = new[] { 0.2, 0.5, 0.8 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void ReturnsOnlyRootsInsideRequestedInterval()
        {
            // (x + 2)(x - 0.25)(x - 3)
            // = (x + 2)(x^2 - 3.25x + 0.75)
            // = x^3 - 1.25x^2 - 5.75x + 1.5.
            // Of the three algebraic roots, only 0.25 is in [0, 1].
            const double a = 1.0;
            const double b = -1.25;
            const double c = -5.75;
            const double d = 1.5;
            var expected = new[] { 0.25 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void ReturnsRepeatedRootOnlyOnce()
        {
            // (x - 0.4)^2(x - 0.9)
            // = (x^2 - 0.8x + 0.16)(x - 0.9)
            // = x^3 - 1.7x^2 + 0.88x - 0.144.
            // The factorization proves that 0.4 is a double root and 0.9 is simple.
            const double a = 1.0;
            const double b = -1.7;
            const double c = 0.88;
            const double d = -0.144;
            var expected = new[] { 0.4, 0.9 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, new[] { 0.4, 0.9 });
            Assert.That(EvaluateDerivative(a, b, c, 0.4), Is.EqualTo(0.0).Within(1e-14),
                "The derivative must also vanish at the stated double root");
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void CollapsesFloatAdjacentCandidatesAroundRepeatedRoot()
        {
            // In exact arithmetic these coefficients expand
            // (x - 0.122)^2(x - 0.172). Computing the expansion in double precision rounds
            // the coefficients independently, slightly splitting the double root.
            const double repeatedRoot = 0.122;
            const double simpleRoot = 0.172;
            const double a = 1.0;
            var b = -(2.0 * repeatedRoot + simpleRoot);
            var c = repeatedRoot * repeatedRoot + 2.0 * repeatedRoot * simpleRoot;
            var d = -repeatedRoot * repeatedRoot * simpleRoot;

            // The rounded polynomial has a positive local maximum near 0.122, but is
            // negative only 10^-8 to either side. It therefore has two numerical roots
            // in this interval, separated by only a few float ULPs. The interval solver
            // must collapse the candidates before converting them to float.
            var derivativeDiscriminant = 4.0 * b * b - 12.0 * a * c;
            var criticalPoint = (-2.0 * b - Math.Sqrt(derivativeDiscriminant)) / (6.0 * a);
            const double splitBracketRadius = 1e-8;
            Assert.That(Evaluate(a, b, c, d, criticalPoint), Is.GreaterThan(0.0));
            Assert.That(Evaluate(a, b, c, d, repeatedRoot - splitBracketRadius), Is.LessThan(0.0));
            Assert.That(Evaluate(a, b, c, d, repeatedRoot + splitBracketRadius), Is.LessThan(0.0));
            Assert.That(
                (float)(repeatedRoot - splitBracketRadius),
                Is.Not.EqualTo((float)(repeatedRoot + splitBracketRadius)),
                "The split neighborhood must span more than one float value"
            );

            var expected = new[] { repeatedRoot, simpleRoot };
            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void ReturnsTripleRootOnlyOnce()
        {
            // (x - 0.3)^3 = x^3 - 0.9x^2 + 0.27x - 0.027.
            const double a = 1.0;
            const double b = -0.9;
            const double c = 0.27;
            const double d = -0.027;
            var expected = new[] { 0.3 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, new[] { 0.3 });
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void SmallDiscriminantDoesNotCreateSpuriousRoots()
        {
            // x^3 + 10^-6 = 0 has the one real root x = -0.01 because
            // (-0.01)^3 = -10^-6. Its other two roots are complex.
            const double a = 1.0;
            const double b = 0.0;
            const double c = 0.0;
            const double d = 1e-6;
            var expected = new[] { -0.01 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, -1.0, 1.0));
        }

        [Test]
        public void ScalingAllCoefficientsDoesNotChangeRoots()
        {
            // Multiplication by a nonzero scalar does not change a polynomial's roots:
            // 10^-200 (x - 0.2)(x - 0.5)(x - 0.8) = 0 has the same roots as the
            // expanded polynomial used by FindsThreeDistinctRootsInInterval.
            const double scale = 1e-200;
            const double a = scale;
            const double b = -1.5 * scale;
            const double c = 0.66 * scale;
            const double d = -0.08 * scale;
            var expected = new[] { 0.2, 0.5, 0.8 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void TinyLeadingCoefficientDoesNotRequireMonicNormalization()
        {
            // x(10^-300 x^2 + x - 1) has the exact root x = 0. The quadratic factor
            // is strictly negative on [-0.1, 0.1], since x - 1 <= -0.9 there and its
            // positive 10^-300 x^2 term is at most 10^-302. Therefore zero is the only
            // root in this interval. Dividing by a would instead create B = 10^300 and
            // overflow when forming B^2 in the former Cardano implementation.
            const double a = 1e-300;
            const double b = 1.0;
            const double c = -1.0;
            const double d = 0.0;
            var expected = new[] { 0.0 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, -0.1, 0.1));
        }

        [Test]
        public void IncludesRootsAtIntervalEndpoints()
        {
            // x(x - 0.5)(x - 1) = x^3 - 1.5x^2 + 0.5x, so both interval
            // endpoints and the midpoint are roots in the closed interval [0, 1].
            const double a = 1.0;
            const double b = -1.5;
            const double c = 0.5;
            const double d = 0.0;
            var expected = new[] { 0.0, 0.5, 1.0 };

            AssertExpectedRootsEvaluateToZero(a, b, c, d, expected);
            AssertRoots(expected, CubicSolver.SolveCubicInterval(a, b, c, d, 0.0, 1.0));
        }

        [Test]
        public void HandlesQuadraticAndLinearInputs()
        {
            // (x - 0.2)(x - 0.8) = x^2 - x + 0.16.
            var quadraticExpected = new[] { 0.2, 0.8 };
            AssertExpectedRootsEvaluateToZero(0.0, 1.0, -1.0, 0.16, quadraticExpected);
            AssertRoots(quadraticExpected,
                CubicSolver.SolveCubicInterval(0.0, 1.0, -1.0, 0.16, 0.0, 1.0));

            // 2x - 1 = 0 iff x = 0.5.
            var linearExpected = new[] { 0.5 };
            AssertExpectedRootsEvaluateToZero(0.0, 0.0, 2.0, -1.0, linearExpected);
            AssertRoots(linearExpected,
                CubicSolver.SolveCubicInterval(0.0, 0.0, 2.0, -1.0, 0.0, 1.0));
        }

        [Test]
        public void ReturnsNoRootWhenPolynomialDoesNotReachZero()
        {
            // x^2 + 1 is at least 1 for every real x, so it has no real roots.
            var roots = CubicSolver.SolveCubicInterval(0.0, 1.0, 0.0, 1.0, -10.0, 10.0);
            Assert.That(roots, Is.Empty);
        }

        private static void AssertExpectedRootsEvaluateToZero(
            double a,
            double b,
            double c,
            double d,
            double[] expectedRoots
        )
        {
            foreach (var root in expectedRoots)
            {
                var residual = Math.Abs(Evaluate(a, b, c, d, root));
                var scale = EvaluationScale(a, b, c, d, root);
                var scaledResidual = scale == 0.0 ? residual : residual / scale;
                Assert.That(scaledResidual, Is.LessThan(1e-14),
                    $"The stated root {root:R} must evaluate to zero for the test polynomial");
            }
        }

        private static void AssertRoots(double[] expected, IEnumerable<float> actualRoots)
        {
            var actual = actualRoots.ToArray();
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(RootTolerance),
                    $"Unexpected root at index {i}");
            }
        }

        private static double Evaluate(double a, double b, double c, double d, double x)
        {
            return ((a * x + b) * x + c) * x + d;
        }

        private static double EvaluateDerivative(double a, double b, double c, double x)
        {
            return (3.0 * a * x + 2.0 * b) * x + c;
        }

        private static double EvaluationScale(double a, double b, double c, double d, double x)
        {
            var absX = Math.Abs(x);
            return ((Math.Abs(a) * absX + Math.Abs(b)) * absX + Math.Abs(c)) * absX + Math.Abs(d);
        }
    }
}
