using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Homestead
{
    [StaticConstructorOnStartup]
    public static class HomesteadStartup
    {
        static HomesteadStartup()
        {
            var harmony = new Harmony("pluscosmic.homestead");

            TryPatch(harmony,
                AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
                    new[] { typeof(Thought_Memory), typeof(Pawn) }),
                prefix: new HarmonyMethod(typeof(HomesteadPatches), nameof(HomesteadPatches.SuppressBedroomMemory_Prefix)));

            TryPatch(harmony,
                AccessTools.Method(typeof(RCellFinder), "CanWanderToCell"),
                postfix: new HarmonyMethod(typeof(HomesteadPatches), nameof(HomesteadPatches.CanWanderToCell_Postfix)));

            TryPatch(harmony,
                AccessTools.Method(typeof(WorkGiver_CleanFilth), "HasJobOnThing",
                    new[] { typeof(Pawn), typeof(Thing), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(HomesteadPatches), nameof(HomesteadPatches.CleanFilth_Postfix)));

            // 1.6 routes internal callers straight to the _NewTemp overload.
            MethodBase haulFast = AccessTools.Method(typeof(HaulAIUtility), "PawnCanAutomaticallyHaulFast_NewTemp")
                ?? AccessTools.Method(typeof(HaulAIUtility), "PawnCanAutomaticallyHaulFast",
                    new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
            TryPatch(harmony, haulFast,
                postfix: new HarmonyMethod(typeof(HomesteadPatches), nameof(HomesteadPatches.HaulFast_Postfix)));

            TryPatch(harmony,
                AccessTools.Method(typeof(JoyGiver_InteractBuilding), "CanInteractWith",
                    new[] { typeof(Pawn), typeof(Thing), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(HomesteadPatches), nameof(HomesteadPatches.CanInteractWith_Postfix)));

            TryPatch(harmony,
                AccessTools.Method(typeof(Need_Rest), nameof(Need_Rest.TickResting)),
                prefix: new HarmonyMethod(typeof(HomesteadPatches), nameof(HomesteadPatches.TickResting_Prefix)));

            InjectDoorBoundaryComp();
            MoodScaler.Apply();
        }

        // Every door (vanilla and modded) gets the outer-door toggle comp.
        private static void InjectDoorBoundaryComp()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass != null && typeof(Building_Door).IsAssignableFrom(def.thingClass))
                {
                    def.comps ??= new System.Collections.Generic.List<CompProperties>();
                    def.comps.Add(new CompProperties_HouseDoorBoundary());
                }
            }
        }

        private static void TryPatch(Harmony harmony, MethodBase target,
            HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            if (target == null)
            {
                Log.Warning("[Homestead] Could not resolve a patch target; a feature will be disabled.");
                return;
            }
            try
            {
                harmony.Patch(target, prefix: prefix, postfix: postfix);
            }
            catch (Exception e)
            {
                Log.Warning($"[Homestead] Failed to patch {target.DeclaringType?.Name}.{target.Name}: {e.Message}");
            }
        }
    }

    public static class HomesteadPatches
    {
        private static ThoughtDef sleptInBedroom;
        private static ThoughtDef sleptInBarracks;
        private static bool sleptDefsResolved;

        private static bool AvoidanceEnabled => HomesteadMod.Settings?.enableAvoidance ?? true;

        // Housed pawns judge their whole house instead of the vanilla wake-up
        // bedroom/barracks memories.
        public static bool SuppressBedroomMemory_Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought)
        {
            if (!(HomesteadMod.Settings?.replaceBedroomThoughts ?? true))
            {
                return true;
            }
            if (!sleptDefsResolved)
            {
                sleptInBedroom = DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInBedroom");
                sleptInBarracks = DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInBarracks");
                sleptDefsResolved = true;
            }
            if (newThought.def != sleptInBedroom && newThought.def != sleptInBarracks)
            {
                return true;
            }
            return HouseManager.HouseOf(__instance.pawn) == null;
        }

        public static void CanWanderToCell_Postfix(ref bool __result, IntVec3 c, Pawn pawn)
        {
            if (!__result || !AvoidanceEnabled || pawn?.Map == null || !pawn.IsFreeColonist)
            {
                return;
            }
            CompHouseMarker house = pawn.Map.GetComponent<HouseManager>()?.GetHouseAt(c);
            if (house != null && house.Owners.Count > 0 && !house.IsMember(pawn))
            {
                __result = false;
            }
        }

        public static void CleanFilth_Postfix(ref bool __result, Pawn pawn, Thing t, bool forced)
        {
            if (!__result || forced || !AvoidanceEnabled)
            {
                return;
            }
            CompHouseMarker house = pawn.Map?.GetComponent<HouseManager>()?.GetHouseAt(t.Position);
            if (house != null && house.Owners.Count > 0 && !house.IsMember(pawn))
            {
                __result = false;
            }
        }

        public static void HaulFast_Postfix(ref bool __result, Pawn p, Thing t, bool forced)
        {
            if (!__result || forced || !AvoidanceEnabled || !p.IsFreeColonist)
            {
                return;
            }
            CompHouseMarker house = p.Map?.GetComponent<HouseManager>()?.GetHouseAt(t.Position);
            if (house != null && house.Owners.Count > 0 && !house.IsMember(p))
            {
                __result = false;
            }
        }

        // There's no place like bed: resting inside your own house is more effective.
        public static void TickResting_Prefix(Pawn ___pawn, ref float restEffectiveness)
        {
            float bonus = HomesteadMod.Settings?.homeRestBonus ?? 0f;
            if (bonus <= 0f || ___pawn == null || !___pawn.Spawned || !___pawn.IsFreeColonist)
            {
                return;
            }
            CompHouseMarker house = ___pawn.Map.GetComponent<HouseManager>()?.GetHouseOf(___pawn);
            if (house != null && house.ContainsCell(___pawn.Position))
            {
                restEffectiveness *= 1f + bonus;
            }
        }

        public static void CanInteractWith_Postfix(ref bool __result, Pawn pawn, Thing t)
        {
            if (!__result || !AvoidanceEnabled || pawn?.Map == null)
            {
                return;
            }
            CompHouseMarker house = pawn.Map.GetComponent<HouseManager>()?.GetHouseAt(t.Position);
            if (house != null && house.Owners.Count > 0 && !house.IsMember(pawn))
            {
                __result = false;
            }
        }
    }
}
