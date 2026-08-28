#nullable enable
using System;
using System.Linq;
using System.Reflection;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class AssemblyCollector
    {
        static Assembly[]? asm_cash;
        public static Assembly[] GetAssemblies()
        {
            if (asm_cash is not null) { return asm_cash; }

#if UNITY_6000_6_OR_NEWER
            asm_cash = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().ToArray();
            return asm_cash;
#else
            asm_cash = AppDomain.CurrentDomain.GetAssemblies();
            return asm_cash;
#endif

        }
    }
}
