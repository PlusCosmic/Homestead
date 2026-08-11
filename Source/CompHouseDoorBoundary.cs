using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Homestead
{
    public class CompProperties_HouseDoorBoundary : CompProperties
    {
        public CompProperties_HouseDoorBoundary()
        {
            compClass = typeof(CompHouseDoorBoundary);
        }
    }

    // Injected into every door def at startup. Marking a door as an "outer door"
    // hard-stops house room claiming from crossing it in either direction.
    public class CompHouseDoorBoundary : ThingComp
    {
        public bool isBoundary;

        private static Texture2D boundaryIcon;

        public static bool IsBoundaryDoor(Thing door)
        {
            return door is ThingWithComps twc
                && twc.GetComp<CompHouseDoorBoundary>() is CompHouseDoorBoundary comp
                && comp.isBoundary;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isBoundary, "houseBoundary");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (parent.Faction != Faction.OfPlayer || !parent.Spawned)
            {
                yield break;
            }
            // Only clutter doors a house actually reaches; always show on doors
            // already marked so the flag can never get stranded.
            HouseManager manager = parent.Map.GetComponent<HouseManager>();
            if (manager == null)
            {
                yield break;
            }
            if (!isBoundary && (parent is not Building_Door door || !manager.IsHouseAdjacentDoor(door)))
            {
                yield break;
            }
            if (boundaryIcon == null)
            {
                boundaryIcon = ContentFinder<Texture2D>.Get("Homestead/UI/OuterDoor");
            }
            yield return new Command_Toggle
            {
                defaultLabel = "Homestead_GizmoOuterDoor".Translate(),
                defaultDesc = "Homestead_GizmoOuterDoorDesc".Translate(),
                icon = boundaryIcon,
                isActive = () => isBoundary,
                toggleAction = () =>
                {
                    isBoundary = !isBoundary;
                    manager.SetDirty();
                },
            };
        }

        public override string CompInspectStringExtra()
        {
            return isBoundary ? "Homestead_InspectOuterDoor".Translate().ToString() : null;
        }
    }
}
