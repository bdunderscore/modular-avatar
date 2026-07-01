using System;
using System.Diagnostics;
using System.Text;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Utility for finding the real roots of a cubic equation / a 1D cubic Bezier curve.
    /// - Does not handle complex roots (intended for cases where the caller guarantees
    ///   only real roots exist).
    /// - Internal computation is done in double; inputs/outputs are float (Unity-friendly,
    ///   targeting precision comparable to AnimationCurve).
    /// - Uses Cardano's formula, switching to the trigonometric method when the
    ///   discriminant indicates 3 real roots, to avoid cancellation error.
    /// - Finishes with a few Newton-Raphson iterations to polish the result.
    /// </summary>
    internal static class CubicSolver
    {
        // SolveCubic

        private class CubicSolverWindow : EditorWindow
        {
            [MenuItem("Window/Modular Avatar Solver")]
            private static void ShowWindow() => GetWindow(typeof(CubicSolverWindow)).Show();

            private double a, b, c, d;

            private void OnGUI()
            {
                a = EditorGUILayout.DoubleField("A", a);
                b = EditorGUILayout.DoubleField("B", b);
                c = EditorGUILayout.DoubleField("C", c);
                d = EditorGUILayout.DoubleField("D", d);
                var sw = Stopwatch.StartNew();
                var result = SolveCubicDouble(a, b, c, d);
                var dur = sw.Elapsed;
                EditorGUILayout.SelectableLabel($"{result}");
                EditorGUILayout.SelectableLabel($"took {dur}");
            }
        }

        private const double Eps = 1e-12;

        /// <summary>
        /// Internal double-precision solver for a x^3 + b x^2 + c x + d = 0.
        /// </summary>
        public static Roots SolveCubicDouble(double a, double b, double c, double d)
        {
            // If a is essentially 0, fall back to a lower-degree solver (quadratic / linear)
            if (Math.Abs(a) < Eps)
            {
                return SolveQuadraticOrLower(b, c, d);
            }

            // Normalize to monic form: x^3 + B x^2 + C x + D = 0
            var B = b / a;
            var C = c / a;
            var D = d / a;

            // Depress the cubic: t^3 + p t + q = 0 (x = t - B/3)
            var shift = B / 3.0;
            var p = C - B * B / 3.0;
            var q = (2.0 * B * B * B) / 27.0 - (B * C) / 3.0 + D;

            var tRoots = SolveDepressedCubic(p, q);

            var roots = Roots.New();
            for (var i = 0; i < tRoots.Length; i++)
                roots.Add(tRoots[i] - shift);

            return roots;
        }

        /// <summary>
        /// Solves the depressed cubic t^3 + p t + q = 0.
        /// Branches between Cardano's formula (1 real root) and the trigonometric
        /// method (3 real roots) based on the discriminant. On the Cardano branch,
        /// u is computed first and v is derived via v = -p/(3u) to avoid cancellation.
        /// </summary>
        private static Roots SolveDepressedCubic(double p, double q)
        {
            // p and q both ~0 -> triple root at t = 0
            if (Math.Abs(p) < Eps && Math.Abs(q) < Eps)
            {
                return new Roots(0.0);
            }

            // D = q^2/4 + p^3/27  (D > 0: 1 real root / D <= 0: 3 real roots, D = 0 includes a repeated root)
            double D = (q * q) / 4.0 + (p * p * p) / 27.0;

            if (D > Eps)
            {
                // ---- 1 real root: Cardano's formula (cancellation-avoiding form) ----
                var sqrtD = Math.Sqrt(D);
                var term = -q / 2.0 + sqrtD;

                var u = CubeRoot(term);
                double v;
                if (Math.Abs(u) > Eps)
                {
                    v = -p / (3.0 * u);
                }
                else
                {
                    // Special case where u is ~0: fall back to the cube root of -q directly
                    v = CubeRoot(-q - u * u * u);
                }

                var t0 = u + v;
                return new Roots(t0);
            }
            else if (D < -Eps)
            {
                // ---- 3 real roots: trigonometric method (casus irreducibilis) ----
                // p is guaranteed to be negative here
                var r = Math.Sqrt(-p / 3.0);
                var cosArg = (3.0 * q) / (2.0 * p) * Math.Sqrt(-3.0 / p);

                // Clamp to avoid going out of range due to floating-point error
                cosArg = Math.Max(-1.0, Math.Min(1.0, cosArg));
                var theta = Math.Acos(cosArg) / 3.0;

                const double twoPiOver3 = 2.0 * Math.PI / 3.0;

                var t0 = 2.0 * r * Math.Cos(theta);
                var t1 = 2.0 * r * Math.Cos(theta - twoPiOver3);
                var t2 = 2.0 * r * Math.Cos(theta - 2.0 * twoPiOver3);

                return new Roots(t0, t1, t2);
            }
            else
            {
                // D ~ 0: repeated-root case (one simple root + one double root)
                var u = CubeRoot(-q / 2.0);
                var t0 = 2.0 * u; // simple root
                var t1 = -u; // double root
                return new Roots(t0, t1, t1);
            }
        }

        /// <summary>
        /// Signed cube root (handles negative inputs).
        /// </summary>
        private static double CubeRoot(double x)
        {
            if (x < 0.0)
            {
                return -Math.Pow(-x, 1.0 / 3.0);
            }

            return Math.Pow(x, 1.0 / 3.0);
        }

        /// <summary>
        /// Fallback for when a is essentially 0 (quadratic / linear equation).
        /// </summary>
        private static Roots SolveQuadraticOrLower(double b, double c, double d)
        {
            if (Math.Abs(b) < Eps)
            {
                // Linear equation: c x + d = 0
                if (Math.Abs(c) < Eps)
                {
                    return Roots.New(); // No solution (or indeterminate; not handled here)
                }

                return new Roots(-d / c);
            }

            // Quadratic equation: b x^2 + c x + d = 0 (cancellation-avoiding form of the quadratic formula)
            var disc = c * c - 4.0 * b * d;
            if (disc < 0.0)
            {
                return Roots.New();
            }

            var sqrtDisc = Math.Sqrt(disc);

            // Compute the addition branch matching the sign of c first, to avoid cancellation
            var q = (c >= 0.0) ? -0.5 * (c + sqrtDisc) : -0.5 * (c - sqrtDisc);

            if (Math.Abs(q) < Eps)
            {
                var x0 = -c / (2.0 * b);
                return new Roots(x0);
            }

            var r0 = q / b;
            var r1 = d / q;
            return r0 <= r1 ? new Roots(r0, r1) : new Roots(r1, r0);
        }

        /// <summary>
        /// Polishes a root using Newton-Raphson iteration, stopping after a few iterations.
        /// </summary>
        private static double NewtonPolish(double a, double b, double c, double d, double x)
        {
            const int maxIter = 6;
            for (int i = 0; i < maxIter; i++)
            {
                double f = ((a * x + b) * x + c) * x + d; // Horner's method
                double fPrime = (3.0 * a * x + 2.0 * b) * x + c;

                if (Math.Abs(fPrime) < Eps)
                {
                    break; // Derivative is ~0 (near a repeated root); stop iterating
                }

                double dx = f / fPrime;
                x -= dx;

                if (Math.Abs(dx) < 1e-15)
                {
                    break; // Converged well enough
                }
            }

            return x;
        }

        public struct Roots
        {
            private double _s0;
            private double _s1;
            private double _s2;

            public Roots(double s0)
            {
                this = New();
                Add(s0);
            }
            
            public Roots(double s0, double s1)
            {
                this = New();
                Add(s0);
                Add(s1);
            }
            
            public Roots(double s0, double s1, double s2)
            {
                this = New();
                Add(s0);
                Add(s1);
                Add(s2);
            }

            public static Roots New() =>
                new()
                {
                    _s0 = double.NaN,
                    _s1 = double.NaN,
                    _s2 = double.NaN,
                };

            public void Add(double x)
            {
                if (double.IsNaN(_s0)) _s0 = x;
                else if (double.IsNaN(_s1)) _s1 = x;
                else if (double.IsNaN(_s2)) _s2 = x;
                else throw new InvalidOperationException("Cannot add more than three solution.");
            }

            public int Length => double.IsNaN(_s0) ? 0 : double.IsNaN(_s1) ? 1 : double.IsNaN(_s2) ? 2 : 3;
            public double this[int i]
            {
                get => i == 0 ? _s0 : i == 1 ? _s1 : i == 2 ? _s2 : throw new IndexOutOfRangeException();
                set
                {
                    switch (i)
                    {
                        case 0:
                            _s0 = value;
                            break;
                        case 1:
                            _s1 = value;
                            break;
                        case 2:
                            _s2 = value;
                            break;
                        default:
                            throw new IndexOutOfRangeException();
                    }
                }
            }

            public double[] ToArray() => Length switch
            {
                0 => Array.Empty<double>(),
                1 => new[] { _s0 },
                2 => new[] { _s0, _s1 },
                3 => new[] { _s0, _s1, _s2 },
            };

            public override string ToString()
            {
                if (double.IsNaN(_s0)) return "{}";
                var builder = new StringBuilder("{");
                builder.Append(_s0);
                if (!double.IsNaN(_s1)) builder.Append($", {_s1}");
                if (!double.IsNaN(_s2)) builder.Append($", {_s2}");
                builder.Append("}");
                return builder.ToString();
            }
        }
    }
}
