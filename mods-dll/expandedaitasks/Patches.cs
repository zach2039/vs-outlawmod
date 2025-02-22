﻿using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using Vintagestory.API.Common;

namespace ExpandedAiTasks
{

    public static class ExpandedAiTasksHarmonyPatcher
    {
        private static Harmony harmony;

        public static bool ShouldPatch()
        {
            return harmony == null;
        }

        public static void ApplyPatches()
        {
            Debug.Assert(ShouldPatch(), "ExpandedAiTasks Harmony patches have already been applied, call ShouldPatch to determine if this method should be called.");
            harmony = new Harmony("com.grifthegnome.expandedaitasks.aitaskpatches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////
    ///PATCHING TO ADD A UNIVERAL SET LOCATION FOR LAST ENTITY TO ATTACK ON ENTITY AGENT//
    //////////////////////////////////////////////////////////////////////////////////////

    [HarmonyPatch(typeof(EntityAgent))]
    public class ReceiveDamageOverride
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            return true;
        }

        [HarmonyPatch("ReceiveDamage")]
        [HarmonyPostfix]
        static void OverrideReceiveDamage(EntityAgent __instance, DamageSource damageSource, float damage)
        {
            if (__instance.Alive)
            {
                AiUtility.SetLastAttacker(__instance, damageSource);
            }
        }
    }
}