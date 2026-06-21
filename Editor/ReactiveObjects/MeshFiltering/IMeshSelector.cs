using System;
using nadena.dev.ndmf.preview;
using Unity.Collections;
using Unity.Jobs;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IMeshSelector : IEquatable<IMeshSelector>
    {
        /// <summary>
        ///     Sets filtered[i] to true for each primitive (tri or quad) in the mesh that is selected by this IMeshSelector.
        ///     Entries corresponding to unmatched prims are left as-is.
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="mesh"></param>
        /// <param name="submesh"></param>
        /// <param name="selectedPrimitives"></param>
        JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives);

        void Observe(ComputeContext context)
        {
        }
    }
}