#region

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class InjectParamsUsageUI
    {
        private static readonly Type type = AccessTools.TypeByName("UnityEditor.PropertyEditor");
        private static readonly PropertyInfo _editorsElement = AccessTools.Property(type, "editorsElement");

        private static readonly Type editorElem = AccessTools.TypeByName("UnityEditor.UIElements.EditorElement");
        private static readonly PropertyInfo editorElem_editor = AccessTools.Property(editorElem, "editor");

        public static void Patch(Harmony h)
        {
            var type = AccessTools.TypeByName("UnityEditor.PropertyEditor");
            var drawEditors = AccessTools.Method(type, "DrawEditors");

            h.Patch(drawEditors, transpiler: new HarmonyMethod(typeof(InjectParamsUsageUI), nameof(Transpile)));

            var objNames = AccessTools.TypeByName("UnityEditor.ObjectNames");
            var m_GetObjectTypeName = AccessTools.Method(objNames, "GetObjectTypeName");
            var postfix_GetObjectTypeName =
                AccessTools.Method(typeof(InjectParamsUsageUI), nameof(Postfix_GetObjectTypeName));

            h.Patch(m_GetObjectTypeName, postfix: new HarmonyMethod(postfix_GetObjectTypeName));
        }

        private static void Postfix_GetObjectTypeName(ref string __result, Object o)
        {
            if (o is ModularAvatarInformation)
            {
                __result = "Modular Avatar Information";
            }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> ci)
        {
            var target = AccessTools.Method(typeof(VisualElement), "Add");

            foreach (var i in ci)
            {
                if (i.opcode != OpCodes.Callvirt)
                {
                    yield return i;
                    continue;
                }

                if (i.opcode == OpCodes.Callvirt
                    && i.operand is MethodInfo method
                    && method == target
                   )
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(InjectParamsUsageUI), nameof(EditorAdd)));
                    continue;
                }

                yield return i;
            }
        }

        private static void EditorAdd(VisualElement container, VisualElement child, object caller)
        {
            container.Add(child);

            var editorsElement = _editorsElement.GetValue(caller) as VisualElement;
            if (editorsElement != container)
            {
                return;
            }

            if (!child.ClassListContains("game-object-inspector"))
            {
                return;
            }

            var editor = editorElem_editor.GetValue(child) as Editor;
            if (editor == null) return;

            if (editor.targets.Length != 1) return;

            if (editor.target is GameObject obj)
            {
                var elem = new ParamsUsageUI();
                container.Add(elem);
            }
        }
    }
}