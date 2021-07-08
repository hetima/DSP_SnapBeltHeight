using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HTSnapBeltHeight
{
    [BepInPlugin(__GUID__, __NAME__, "1.0.0")]
    public class SnapBeltHeight : BaseUnityPlugin
    {
        public const string __NAME__ = "SnapBeltHeight";
        public const string __GUID__ = "com.hetima.dsp." + __NAME__;

        new internal static ManualLogSource Logger;
        void Awake()
        {
            Logger = base.Logger;
            //Logger.LogInfo("Awake");

            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }


        static class Patch
        {

            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Path), "DeterminePreviews")]
            public static IEnumerable<CodeInstruction> BuildTool_Path_DeterminePreviews_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                //if (VFInput._beltsZeroKey.onDown)
                //IL_003d: call valuetype VFInput / InputValue VFInput::get__beltsZeroKey()
                //IL_0042: ldfld        bool VFInput/ InputValue::onDown
                //IL_0047: brfalse.s IL_0050

                //this.altitude = 0;
                //IL_0049: ldarg.0      // this
                //IL_004a: ldc.i4.0 //ここをcallに変える
                //IL_004b: stfld int32 BuildTool_Path::altitude

                MethodInfo anchor = typeof(VFInput).GetMethod("get__beltsZeroKey");
                MethodInfo rep = typeof(SnapBeltHeight).GetMethod("BeltsZeroPatch");

                bool ready = false;
                bool patched = false;

                foreach (var ins in instructions)
                {
                    if (!patched && !ready && ins.opcode == OpCodes.Call && ins.operand is MethodInfo o && o == anchor)
                    {
                        ready = true;
                    }
                    if (!patched && ready && ins.opcode == OpCodes.Ldc_I4_0)
                    {
                        patched = true;
                        yield return new CodeInstruction(OpCodes.Call, rep);
                    }
                    else
                    {
                        yield return ins;
                    }
                }
            }
        }

        public static int ObjectAltitude(Vector3 pos)
        {
            PlanetAuxData aux = GameMain.mainPlayer.controller.actionBuild.planetAux;
            if (aux ==null)
            {
                return 0;
            }
            //Snapの第2引数をtrueにすると地面にスナップする
            Vector3 ground = aux.Snap(pos, true);
            float distance = Vector3.Distance(pos, ground);
            return (int)Math.Round(distance / PlanetGrid.kAltGrid);
        }

        public static int BeltsZeroPatch()
        {
            int result = 0;
            BuildTool_Path tool = GameMain.mainPlayer.controller.actionBuild.pathTool;
            //ストレージにも対応したいけど castObject に入ってこない
            if (ObjectIsBeltOrSplitter(tool, tool.castObjectId))
            {
                result = ObjectAltitude(tool.castObjectPos);
                //Logger.LogInfo("distance:" + distance + " / Altitude:" + result);
            }
            //開始地点と地面をトグル
            else if (tool.altitude == 0)
            {
                result = ObjectAltitude(tool.pathPoints[0]);
            }

            return result;
        }

        
        static public bool ObjectIsBeltOrSplitter(BuildTool_Path tool, int objId)
        {
            if (objId == 0)
            {
                return false;
            }
            ItemProto proto;
            if (objId > 0)
            {
                proto = LDB.items.Select(tool.factory.entityPool[objId].protoId);
            }
            else
            {
                proto = LDB.items.Select(tool.factory.prebuildPool[-objId].protoId);
            }

            if (proto == null || proto.prefabDesc == null)
            {
                return false;
            }
            if (proto.prefabDesc.isBelt || proto.prefabDesc.isSplitter)
            {
                return true;
            }
            return false;
        }
    }
}
