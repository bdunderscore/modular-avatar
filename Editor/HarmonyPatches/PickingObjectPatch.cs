#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class PickingObjectPatch
    {
        private static Type t_PickingObject = AccessTools.TypeByName("UnityEditor.PickingObject");

        private static Type l_PickingObject =
            typeof(List<>).MakeGenericType(new[] { t_PickingObject });

        private static ConstructorInfo ctor_l = AccessTools.Constructor(l_PickingObject);

        private static ConstructorInfo ctor_PickingObject =
            AccessTools.Constructor(t_PickingObject, new[] { typeof(Object), typeof(int) });

        private static PropertyInfo p_materialIndex = AccessTools.Property(t_PickingObject, "materialIndex");

        private static MethodInfo m_TryGetGameObject = AccessTools.Method(t_PickingObject, "TryGetGameObject");

        internal static void Patch(Harmony h)
        {
            var t_PickingObject = AccessTools.TypeByName("UnityEditor.PickingObject");
            var ctor_PickingObject = AccessTools.Constructor(t_PickingObject, new[] { typeof(Object), typeof(int) });

            var t_SceneViewPicking = AccessTools.TypeByName("UnityEditor.SceneViewPicking");
            var m_GetAllOverlapping = AccessTools.Method(t_SceneViewPicking, "GetAllOverlapping");

            var m_postfix = AccessTools.Method(typeof(PickingObjectPatch), nameof(Postfix_GetAllOverlapping));

            h.Patch(original: m_GetAllOverlapping, postfix: new HarmonyMethod(m_postfix));
        }

        private static void Postfix_GetAllOverlapping(ref object __result)
        {
            var erased = (IEnumerable)__result;
            var list = (IList)ctor_l.Invoke(new object[0]);

            foreach (var obj in erased)
            {
                if (obj == null)
                {
                    list.Add(obj);
                    continue;
                }

                var args = new object[] { null };
                if ((bool)m_TryGetGameObject.Invoke(obj, args))
                {
                    var go = args[0] as GameObject;
                    if (go != null && ProxyManager.ProxyToOriginalObject.TryGetValue(go, out var original))
                    {
                        list.Add(ctor_PickingObject.Invoke(new[]
                        {
                            original,
                            p_materialIndex.GetValue(obj)
                        }));
                        continue;
                    }
                }

                list.Add(obj);
            }

            __result = list;
        }
    }
}