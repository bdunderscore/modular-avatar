using System;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    public class PhysBonesTest
    {
        [MenuItem("Tools/Activate PBs")]
        public static void ActivatePBs()
        {
            var go = new GameObject("PB manager");
            var mgr = go.AddComponent<EditModePBManager>();
            mgr.IsSDK = true;
            PhysBoneManager.Inst = mgr;
            mgr.Init();

            foreach (var obj in Selection.activeGameObject.GetComponentsInChildren<VRCPhysBoneBase>())
            {
                obj.SetIsGrabbed(false);
                obj.SetIsPosed(false);
                obj.chainId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
                PhysBoneManager.Inst.AddPhysBone(obj);
                AccessTools.Method(typeof(VRCPhysBoneBase), "InitColliders")
                    .Invoke(obj, new object[] { });
            }
        }
    }
}