using System;
using nadena.dev.ndmf.animator;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public sealed class BakeContext
    {
        public const string ALWAYS_ONE = "$$MA/RC/AlwaysOne";
        public CloneContext CloneContext { get; private set; }
        public VirtualMotion EmptyMotion { get; private set; }
        private readonly VirtualAnimatorController vac;
        private int counter;

        public int Latency { get; private set; }

        public BakeContext(CloneContext cloneContext, VirtualAnimatorController vac)
        {
            CloneContext = cloneContext;
            EmptyMotion = VirtualClip.Create("Empty");
            this.vac = vac;

            vac.Parameters = vac.Parameters.Add(ALWAYS_ONE, new AnimatorControllerParameter
            {
                name = ALWAYS_ONE,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1
            });
        }

        public string AddParameter(string prefix, float value)
        {
            var template = new AnimatorControllerParameter
            {
                name = "$$MA/RC/" + prefix + "$" + counter++,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = value
            };

            vac.Parameters = vac.Parameters.Add(template.name, template);

            return template.name;
        }

        public IDisposable LatencyScope(int frames)
        {
            var scope = new LatencyDisposable(this);
            Latency += frames;
            return scope;
        }

        private class LatencyDisposable : IDisposable
        {
            private readonly BakeContext context;
            private readonly int OriginalLatency;

            public LatencyDisposable(BakeContext context)
            {
                this.context = context;
                OriginalLatency = context.Latency;
            }

            public void Dispose()
            {
                context.Latency = OriginalLatency;
            }
        }
    }
}