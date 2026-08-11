using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Homestead
{
    public class WorkGiver_CleanOwnHouse : WorkGiver_Scanner
    {
        private const int MinTicksSinceThickened = 600;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Filth);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            CompHouseMarker house = HouseManager.HouseOf(pawn);
            return house == null || !house.parent.Spawned || house.parent.Map != pawn.Map;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Filth filth) || filth.Map != pawn.Map)
            {
                return false;
            }
            CompHouseMarker house = HouseManager.HouseOf(pawn);
            if (house == null || !house.ContainsCell(filth.Position))
            {
                return false;
            }
            if (!forced && filth.TicksSinceThickened < MinTicksSinceThickened)
            {
                return false;
            }
            return pawn.CanReserve(t, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Clean);
            job.AddQueuedTarget(TargetIndex.A, t);
            // Sweep up nearby filth in the same house in one trip, like vanilla cleaning does.
            int queued = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(t.Position, 6f, useCenter: false))
            {
                if (!cell.InBounds(t.Map))
                {
                    continue;
                }
                List<Thing> things = cell.GetThingList(t.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] != t && things[i] is Filth && HasJobOnThing(pawn, things[i], forced))
                    {
                        job.AddQueuedTarget(TargetIndex.A, things[i]);
                        queued++;
                    }
                }
                if (queued >= 15)
                {
                    break;
                }
            }
            if (job.targetQueueA != null && job.targetQueueA.Count >= 5)
            {
                job.targetQueueA.SortBy(targ => targ.Cell.DistanceToSquared(pawn.Position));
            }
            return job;
        }
    }

    public class WorkGiver_RepairOwnHouse : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerBuildingsRepairable.RepairableBuildings(pawn.Faction);
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            CompHouseMarker house = HouseManager.HouseOf(pawn);
            return house == null || !house.parent.Spawned || house.parent.Map != pawn.Map;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building building) || building.Faction != pawn.Faction)
            {
                return false;
            }
            if (building.HitPoints >= building.MaxHitPoints || !building.def.building.repairable)
            {
                return false;
            }
            CompHouseMarker house = HouseManager.HouseOf(pawn);
            if (house == null || !HousePlusWalls(house, building))
            {
                return false;
            }
            if (building.IsBurning() || building.IsForbidden(pawn))
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(building, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }
            return pawn.CanReserve(building, 1, -1, null, forced);
        }

        // A house's walls and doors sit on border cells, not interior cells; owners
        // should fix those too.
        private static bool HousePlusWalls(CompHouseMarker house, Building building)
        {
            foreach (IntVec3 cell in building.OccupiedRect())
            {
                if (house.ContainsCell(cell))
                {
                    return true;
                }
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 adj = cell + GenAdj.CardinalDirections[i];
                    if (adj.InBounds(building.Map) && house.ContainsCell(adj))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDefOf.Repair, t);
        }
    }
}
