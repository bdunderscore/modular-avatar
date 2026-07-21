using System;
using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Finds the real roots of a cubic polynomial within a finite interval.
    ///
    /// The polynomial is partitioned at the roots of its derivative. It is monotonic
    /// between adjacent partition points, so a sign change identifies exactly one root.
    /// Roots in those intervals are found with safeguarded Newton iteration, falling
    /// back to bisection whenever a Newton step would leave the bracket.
    /// </summary>
    internal static class CubicSolver
    {
        // The distance from 1.0 to the next representable IEEE-754 binary64 value.
        // System.Double.Epsilon is the smallest positive subnormal value, not this value.
        private const double MachineEpsilon = 2.2204460492503131e-16;
        private const double FloatMachineEpsilon = 1.1920928955078125e-7;
        private const double ResidualToleranceFactor = 64.0;
        private const double PositionToleranceFactor = 16.0;
        private const double DuplicateToleranceFactor = 4.0;
        private const int MaxIterations = 80;

        /// <summary>
        /// Finds the distinct real roots of
        /// <c>a x^3 + b x^2 + c x + d = 0</c> in the closed interval
        /// <paramref name="min"/> through <paramref name="max"/>, in ascending order.
        /// </summary>
        public static IEnumerable<float> SolveCubicInterval(
            double a,
            double b,
            double c,
            double d,
            double min,
            double max
        )
        {
            ValidateFinite(a, nameof(a));
            ValidateFinite(b, nameof(b));
            ValidateFinite(c, nameof(c));
            ValidateFinite(d, nameof(d));
            ValidateFinite(min, nameof(min));
            ValidateFinite(max, nameof(max));

            if (min > max)
                throw new ArgumentException("The root interval must have min <= max.");

            NormalizeCoefficients(ref a, ref b, ref c, ref d);

            if (a == 0.0 && b == 0.0 && c == 0.0)
                return Array.Empty<float>();

            if (min == max)
                return IsRoot(a, b, c, d, min) ? new[] { (float)min } : Array.Empty<float>();

            var distinctRoots = SolvePolynomialInInterval(a, b, c, d, min, max);
            return ConvertToFloats(distinctRoots);
        }

        private static IEnumerable<float> ConvertToFloats(double[] roots)
        {
            var result = new float[roots.Length];
            for (var i = 0; i < roots.Length; i++)
                result[i] = checked((float)roots[i]);

            return result;
        }

        /// <summary>
        /// Solves a polynomial of degree at most three. The derivative is recursively
        /// solved over the same interval, avoiding both monic normalization and a
        /// discriminant calculation.
        /// </summary>
        private static double[] SolvePolynomialInInterval(
            double a,
            double b,
            double c,
            double d,
            double min,
            double max
        )
        {
            NormalizeCoefficients(ref a, ref b, ref c, ref d);

            double[] criticalPoints;
            if (a != 0.0)
            {
                criticalPoints = SolvePolynomialInInterval(0.0, 3.0 * a, 2.0 * b, c, min, max);
            }
            else if (b != 0.0)
            {
                criticalPoints = SolvePolynomialInInterval(0.0, 0.0, 2.0 * b, c, min, max);
            }
            else if (c != 0.0)
            {
                criticalPoints = Array.Empty<double>();
            }
            else
            {
                // A nonzero constant has no roots. The all-zero polynomial has an
                // indeterminate number of roots and is represented by an empty result.
                return Array.Empty<double>();
            }

            var partitions = new List<double>(criticalPoints.Length + 2) { min };
            foreach (var criticalPoint in criticalPoints)
            {
                if (criticalPoint > min && criticalPoint < max)
                    partitions.Add(criticalPoint);
            }

            partitions.Add(max);
            partitions.Sort();

            var values = new double[partitions.Count];
            var roots = new List<double>(3);

            for (var i = 0; i < partitions.Count; i++)
            {
                var x = partitions[i];
                var value = Evaluate(a, b, c, d, x);
                values[i] = value;

                // This also detects even-multiplicity roots, which do not produce a
                // sign change but must occur at a derivative root.
                if (IsRoot(a, b, c, d, x, value))
                    roots.Add(x);
            }

            for (var i = 0; i + 1 < partitions.Count; i++)
            {
                if (!HaveOppositeSigns(values[i], values[i + 1]))
                    continue;

                roots.Add(FindBracketedRoot(
                    a,
                    b,
                    c,
                    d,
                    partitions[i],
                    partitions[i + 1],
                    values[i]
                ));
            }

            return SortAndDeduplicateRoots(roots, a, b, c, d);
        }

        private static double FindBracketedRoot(
            double a,
            double b,
            double c,
            double d,
            double left,
            double right,
            double leftValue
        )
        {
            var x = Midpoint(left, right);

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                var value = Evaluate(a, b, c, d, x);
                if (IsRoot(a, b, c, d, x, value))
                    return x;

                if (HaveSameSign(value, leftValue))
                {
                    left = x;
                    leftValue = value;
                }
                else
                {
                    right = x;
                }

                if (PositionsAreClose(left, right))
                    return BetterRootCandidate(a, b, c, d, left, right);

                var derivative = EvaluateDerivative(a, b, c, x);
                var candidate = x - value / derivative;

                if (!IsFinite(candidate) || candidate <= left || candidate >= right || candidate == x)
                    candidate = Midpoint(left, right);

                // There are no representable values remaining strictly inside the bracket.
                if (candidate == left || candidate == right)
                    return BetterRootCandidate(a, b, c, d, left, right);

                x = candidate;
            }

            return BetterRootCandidate(a, b, c, d, left, right);
        }

        private static double[] SortAndDeduplicateRoots(
            List<double> roots,
            double a,
            double b,
            double c,
            double d
        )
        {
            if (roots.Count == 0)
                return Array.Empty<double>();

            roots.Sort();
            var distinctRoots = new List<double>(roots.Count) { roots[0] };

            for (var i = 1; i < roots.Count; i++)
            {
                var previousIndex = distinctRoots.Count - 1;
                var previous = distinctRoots[previousIndex];
                var current = roots[i];

                if (!RootsAreClose(previous, current))
                {
                    distinctRoots.Add(current);
                    continue;
                }

                // Multiple search paths can report the same numerical root. Keep the
                // representative with the smaller backward error.
                if (ScaledResidual(a, b, c, d, current) < ScaledResidual(a, b, c, d, previous))
                    distinctRoots[previousIndex] = current;
            }

            return distinctRoots.ToArray();
        }

        private static bool IsRoot(double a, double b, double c, double d, double x)
        {
            return IsRoot(a, b, c, d, x, Evaluate(a, b, c, d, x));
        }

        private static bool IsRoot(double a, double b, double c, double d, double x, double value)
        {
            if (!IsFinite(value))
                return false;

            var scale = EvaluationScale(a, b, c, d, x);
            if (!IsFinite(scale))
                return false;

            if (scale == 0.0)
                return value == 0.0;

            return Math.Abs(value) <= ResidualToleranceFactor * MachineEpsilon * scale;
        }

        private static double ScaledResidual(double a, double b, double c, double d, double x)
        {
            var value = Math.Abs(Evaluate(a, b, c, d, x));
            var scale = EvaluationScale(a, b, c, d, x);

            if (scale == 0.0)
                return value == 0.0 ? 0.0 : double.PositiveInfinity;

            return value / scale;
        }

        private static double BetterRootCandidate(
            double a,
            double b,
            double c,
            double d,
            double first,
            double second
        )
        {
            // Scaling is necessary here: comparing raw |f(x)| would make the selected
            // endpoint depend on an arbitrary common scale applied to the coefficients.
            return ScaledResidual(a, b, c, d, first) <= ScaledResidual(a, b, c, d, second)
                ? first
                : second;
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

        private static void NormalizeCoefficients(ref double a, ref double b, ref double c, ref double d)
        {
            var scale = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), Math.Max(Math.Abs(c), Math.Abs(d)));
            if (scale == 0.0)
                return;

            a /= scale;
            b /= scale;
            c /= scale;
            d /= scale;
        }

        private static bool PositionsAreClose(double first, double second)
        {
            var scale = Math.Max(1.0, Math.Max(Math.Abs(first), Math.Abs(second)));
            return Math.Abs(first - second) <= PositionToleranceFactor * MachineEpsilon * scale;
        }

        private static bool RootsAreClose(double first, double second)
        {
            // The public result is float-valued. Treat roots separated by only a few
            // float ULPs as one numerical root. Otherwise coefficient rounding around
            // a repeated root can produce several adjacent float values.
            var scale = Math.Max(1.0, Math.Max(Math.Abs(first), Math.Abs(second)));
            return Math.Abs(first - second) <= DuplicateToleranceFactor * FloatMachineEpsilon * scale;
        }

        private static bool HaveOppositeSigns(double first, double second)
        {
            return (first < 0.0 && second > 0.0) || (first > 0.0 && second < 0.0);
        }

        private static bool HaveSameSign(double first, double second)
        {
            return (first < 0.0 && second < 0.0) || (first > 0.0 && second > 0.0);
        }

        private static double Midpoint(double first, double second)
        {
            // This form cannot overflow when the endpoints have large opposite signs.
            return first * 0.5 + second * 0.5;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (!IsFinite(value))
                throw new ArgumentOutOfRangeException(parameterName, "Cubic solver inputs must be finite.");
        }
    }
}
