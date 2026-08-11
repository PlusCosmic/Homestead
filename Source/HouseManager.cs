using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Homestead
{
    public class HouseManager : MapComponent
    {
        private readonly List<CompHouseMarker> markers = new List<CompHouseMarker>();
        private readonly Dictionary<Room, CompHouseMarker> roomToHouse = new Dictionary<Room, CompHouseMarker>();
        private readonly Dictionary<IntVec3, CompHouseMarker> yardToHouse = new Dictionary<IntVec3, CompHouseMarker>();
        private readonly Dictionary<Pawn, CompHouseMarker> pawnToHouse = new Dictionary<Pawn, CompHouseMarker>();

        private bool dirty = true;
        private int lastRebuildTick = -99999;

        private const int RebuildInterval = 250;
        private const int StrangerScanInterval = 250;
        private const int RelaxScanInterval = 2500;

        private static List<JobDef> exemptJobs;

        public HouseManager(Map map) : base(map)
        {
        }

        public List<CompHouseMarker> Markers => markers;

        public void Register(CompHouseMarker comp)
        {
            if (!markers.Contains(comp))
            {
                markers.Add(comp);
                markers.SortBy(m => m.parent.thingIDNumber);
            }
            SetDirty();
        }

        public void Unregister(CompHouseMarker comp)
        {
            markers.Remove(comp);
            SetDirty();
        }

        public void SetDirty() => dirty = true;

        // --- Queries ---

        public CompHouseMarker GetHouseAt(IntVec3 cell)
        {
            RebuildIfNeeded();
            Room room = cell.GetRoom(map);
            if (room != null && roomToHouse.TryGetValue(room, out CompHouseMarker house))
            {
                return house;
            }
            return yardToHouse.TryGetValue(cell, out CompHouseMarker yardHouse) ? yardHouse : null;
        }

        public CompHouseMarker GetHouseOf(Pawn pawn)
        {
            RebuildIfNeeded();
            return pawnToHouse.TryGetValue(pawn, out CompHouseMarker house) ? house : null;
        }

        public IEnumerable<Pawn> HousedPawns
        {
            get
            {
                RebuildIfNeeded();
                return pawnToHouse.Keys;
            }
        }

        // House of a pawn regardless of which map the house is on.
        public static CompHouseMarker HouseOf(Pawn pawn)
        {
            if (pawn == null || Find.Maps == null)
            {
                return null;
            }
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                CompHouseMarker house = maps[i].GetComponent<HouseManager>()?.GetHouseOf(pawn);
                if (house != null)
                {
                    return house;
                }
            }
            return null;
        }

        // A door touches some house's claimed rooms (so the outer-door toggle is relevant).
        public bool IsHouseAdjacentDoor(Building_Door door)
        {
            RebuildIfNeeded();
            if (roomToHouse.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < 4; i++)
            {
                IntVec3 cell = door.Position + GenAdj.CardinalDirections[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }
                Room room = cell.GetRoom(map);
                if (room != null && roomToHouse.ContainsKey(room))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsGuestOf(Pawn pawn, CompHouseMarker house)
        {
            return pawn.CurJob != null
                && pawn.CurJob.def == HomesteadDefOf.Homestead_VisitFriendHome
                && pawn.CurJob.targetC.Thing == house.parent;
        }

        // --- Rebuild ---

        public void RebuildIfNeeded()
        {
            int now = Find.TickManager.TicksGame;
            if (!dirty && now - lastRebuildTick < RebuildInterval)
            {
                return;
            }
            dirty = false;
            lastRebuildTick = now;

            roomToHouse.Clear();
            yardToHouse.Clear();
            pawnToHouse.Clear();

            var settings = HomesteadMod.Settings;
            var markerRooms = new HashSet<Room>();
            foreach (CompHouseMarker marker in markers)
            {
                Room room = marker.parent.Spawned ? marker.parent.Position.GetRoom(map) : null;
                if (room != null)
                {
                    markerRooms.Add(room);
                }
            }

            // Multi-source BFS over rooms: FIFO processing of markers in thingID order
            // claims each room for its nearest marker (ties go to the older marker).
            var queue = new Queue<(Room room, CompHouseMarker house, int depth)>();
            foreach (CompHouseMarker marker in markers)
            {
                marker.cachedRooms.Clear();
                marker.cachedInteriorCells.Clear();
                if (!marker.parent.Spawned)
                {
                    continue;
                }
                Room startRoom = marker.parent.Position.GetRoom(map);
                if (startRoom == null || startRoom.PsychologicallyOutdoors || roomToHouse.ContainsKey(startRoom))
                {
                    continue;
                }
                roomToHouse[startRoom] = marker;
                marker.cachedRooms.Add(startRoom);
                if (marker.expandThroughDoors)
                {
                    queue.Enqueue((startRoom, marker, 0));
                }
            }

            int claimDepth = settings?.claimDepth ?? 3;
            int maxRoomCells = settings?.maxRoomCells ?? 225;
            while (queue.Count > 0)
            {
                (Room room, CompHouseMarker house, int depth) = queue.Dequeue();
                if (depth >= claimDepth)
                {
                    continue;
                }
                foreach (Room neighbor in NeighborRoomsThroughDoors(room))
                {
                    if (roomToHouse.ContainsKey(neighbor)
                        || neighbor.PsychologicallyOutdoors
                        || neighbor.CellCount > maxRoomCells
                        || markerRooms.Contains(neighbor)
                        || IsSomeoneElsesBedroom(neighbor, house))
                    {
                        continue;
                    }
                    roomToHouse[neighbor] = house;
                    house.cachedRooms.Add(neighbor);
                    queue.Enqueue((neighbor, house, depth + 1));
                }
            }

            foreach (CompHouseMarker marker in markers)
            {
                foreach (Room room in marker.cachedRooms)
                {
                    foreach (IntVec3 cell in room.Cells)
                    {
                        marker.cachedInteriorCells.Add(cell);
                    }
                }
                foreach (IntVec3 cell in marker.yardCells)
                {
                    if (!yardToHouse.ContainsKey(cell))
                    {
                        yardToHouse[cell] = marker;
                    }
                }
                foreach (Pawn pawn in marker.Owners)
                {
                    if (pawn != null && !pawnToHouse.ContainsKey(pawn))
                    {
                        pawnToHouse[pawn] = marker;
                    }
                }
                if (marker.Owners.Count > 0)
                {
                    Current.Game?.GetComponent<HomesteadGameComponent>()?.Notify_HouseExists();
                }
            }
        }

        private IEnumerable<Room> NeighborRoomsThroughDoors(Room room)
        {
            var seen = new HashSet<Room>();
            foreach (IntVec3 border in room.BorderCells)
            {
                Building_Door door = border.GetDoor(map);
                if (door == null || CompHouseDoorBoundary.IsBoundaryDoor(door))
                {
                    continue;
                }
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 beyond = door.Position + GenAdj.CardinalDirections[i];
                    if (!beyond.InBounds(map))
                    {
                        continue;
                    }
                    Room other = beyond.GetRoom(map);
                    if (other != null && other != room && seen.Add(other))
                    {
                        yield return other;
                    }
                }
            }
        }

        // A room with beds assigned to pawns entirely outside this household belongs
        // to someone else; never absorb it. An unowned house has no "someone else"
        // yet (and no gameplay effects), so preview the full dwelling instead.
        private static bool IsSomeoneElsesBedroom(Room room, CompHouseMarker house)
        {
            List<Pawn> owners = house.Owners;
            if (owners.Count == 0)
            {
                return false;
            }
            foreach (Building_Bed bed in room.ContainedBeds)
            {
                foreach (Pawn sleeper in bed.OwnersForReading)
                {
                    if (!owners.Contains(sleeper))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // --- Ticking ---

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (markers.Count == 0)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick % StrangerScanInterval == 17)
            {
                StrangerScan();
            }
            if (tick % RelaxScanInterval == 33)
            {
                RelaxedAtHomeScan();
            }
        }

        private static void BuildExemptJobs()
        {
            exemptJobs = new List<string>
            {
                "BeatFire", "ExtinguishSelf", "Rescue", "TendPatient", "FeedPatient",
                "TakeWoundedPrisonerToBed", "DeliverToBed", "CarryToCryptosleepCasket",
                "Repair", "FixBrokenDownBuilding", "Deconstruct", "Uninstall", "FinishFrame",
                "PlaceNoCostFrame", "SmoothFloor", "SmoothWall", "RemoveFloor", "BuildRoof",
                "RemoveRoof", "Clean", "ClearSnow", "HaulToCell", "HaulToContainer",
                "Refuel", "RefuelAtomic", "Flick", "Arrest", "Capture", "EscortPrisonerToBed",
                "Ingest", "LayDown", "Lovin", "GotoWander",
            }.Select(DefDatabase<JobDef>.GetNamedSilentFail).Where(d => d != null).ToList();
        }

        private void StrangerScan()
        {
            if (exemptJobs == null)
            {
                BuildExemptJobs();
            }
            RebuildIfNeeded();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn intruder = colonists[i];
                CompHouseMarker house = GetHouseAt(intruder.Position);
                if (house == null || house.Owners.Count == 0 || house.IsMember(intruder)
                    || IsGuestOf(intruder, house) || intruder.Drafted)
                {
                    continue;
                }
                Job job = intruder.CurJob;
                if (job != null && (job.playerForced || exemptJobs.Contains(job.def)))
                {
                    continue;
                }
                // Sleeping guests in a spare bed the owner assigned are covered by
                // the bed-ownership rule upstream; anyone else unsettles the owners
                // who are actually home to see them.
                foreach (Pawn owner in house.Owners)
                {
                    if (owner.Spawned && owner.Map == map && owner.Awake() && house.ContainsCell(owner.Position))
                    {
                        owner.needs?.mood?.thoughts?.memories?.TryGainMemory(
                            HomesteadDefOf.Homestead_StrangerInMyHouse, intruder);
                    }
                }
            }
        }

        private void RelaxedAtHomeScan()
        {
            RebuildIfNeeded();
            foreach (KeyValuePair<Pawn, CompHouseMarker> kv in pawnToHouse)
            {
                Pawn pawn = kv.Key;
                if (!pawn.Spawned || pawn.Map != map || !pawn.Awake())
                {
                    continue;
                }
                if (pawn.CurJob?.def.joyKind == null)
                {
                    continue;
                }
                if (kv.Value.ContainsCell(pawn.Position))
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(HomesteadDefOf.Homestead_RelaxedAtHome);
                }
            }
        }
    }
}
