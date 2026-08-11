using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Homestead
{
    public class CompProperties_HouseMarker : CompProperties
    {
        public CompProperties_HouseMarker()
        {
            compClass = typeof(CompHouseMarker);
        }
    }

    public class CompHouseMarker : ThingComp, IRenameable
    {
        private string customName;
        public bool expandThroughDoors = true;
        public List<IntVec3> yardCells = new List<IntVec3>();

        // Populated by HouseManager on rebuild; never saved.
        public List<Room> cachedRooms = new List<Room>();
        public HashSet<IntVec3> cachedInteriorCells = new HashSet<IntVec3>();

        private float cachedImpressiveness;
        private float cachedCleanliness;
        private int statsCacheTick = -99999;

        private static Texture2D renameIcon;
        private static Texture2D yardAddIcon;
        private static Texture2D yardRemoveIcon;
        private static Texture2D expandIcon;

        public CompAssignableToPawn Assignable => parent.GetComp<CompAssignableToPawn>();

        public List<Pawn> Owners => Assignable?.AssignedPawnsForReading ?? new List<Pawn>();

        public bool IsMember(Pawn pawn) => pawn != null && Owners.Contains(pawn);

        public HouseManager Manager => parent.Spawned ? parent.Map.GetComponent<HouseManager>() : null;

        // --- IRenameable ---

        public string RenamableLabel
        {
            get => customName ?? BaseLabel;
            set => customName = value;
        }

        public string BaseLabel
        {
            get
            {
                Pawn first = Owners.FirstOrDefault();
                if (first == null)
                {
                    return "Homestead_UnclaimedHouse".Translate();
                }
                string surname = (first.Name as NameTriple)?.Last ?? first.LabelShort;
                return "Homestead_DefaultHouseName".Translate(surname);
            }
        }

        public string InspectLabel => RenamableLabel;

        public string HouseName => RenamableLabel;

        // --- House shape ---

        public void EnsureCacheFresh()
        {
            Manager?.RebuildIfNeeded();
        }

        public bool ContainsCell(IntVec3 cell)
        {
            EnsureCacheFresh();
            return cachedInteriorCells.Contains(cell) || yardCells.Contains(cell);
        }

        public bool ContainsPawn(Pawn pawn) => pawn.Spawned && pawn.Map == parent.MapHeld && ContainsCell(pawn.Position);

        public int InteriorCellCount
        {
            get
            {
                EnsureCacheFresh();
                return cachedInteriorCells.Count;
            }
        }

        public int RoomCount
        {
            get
            {
                EnsureCacheFresh();
                return cachedRooms.Count;
            }
        }

        public bool IsFunctional
        {
            get
            {
                EnsureCacheFresh();
                return parent.Spawned && cachedRooms.Count > 0;
            }
        }

        // --- Stats ---

        private void RecalcStatsIfNeeded()
        {
            EnsureCacheFresh();
            int now = Find.TickManager.TicksGame;
            if (now - statsCacheTick < 250)
            {
                return;
            }
            statsCacheTick = now;
            float impSum = 0f;
            float cleanSum = 0f;
            int cells = 0;
            foreach (Room room in cachedRooms)
            {
                if (room == null || room.Dereferenced)
                {
                    continue;
                }
                int c = room.CellCount;
                impSum += room.GetStat(RoomStatDefOf.Impressiveness) * c;
                cleanSum += room.GetStat(RoomStatDefOf.Cleanliness) * c;
                cells += c;
            }
            cachedImpressiveness = cells > 0 ? impSum / cells : 0f;
            cachedCleanliness = cells > 0 ? cleanSum / cells : 0f;
        }

        public float AvgImpressiveness
        {
            get
            {
                RecalcStatsIfNeeded();
                return cachedImpressiveness;
            }
        }

        public float AvgCleanliness
        {
            get
            {
                RecalcStatsIfNeeded();
                return cachedCleanliness;
            }
        }

        public int ImpressivenessStageIndex
        {
            get
            {
                RoomStatDef def = RoomStatDefOf.Impressiveness;
                RoomStatScoreStage stage = def.GetScoreStage(AvgImpressiveness);
                return def.scoreStages.IndexOf(stage);
            }
        }

        public string ImpressivenessStageLabel =>
            RoomStatDefOf.Impressiveness.GetScoreStage(AvgImpressiveness)?.label ?? string.Empty;

        // --- Lifecycle ---

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map.GetComponent<HouseManager>()?.Register(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            map.GetComponent<HouseManager>()?.Unregister(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Values.Look(ref expandThroughDoors, "expandThroughDoors", true);
            Scribe_Collections.Look(ref yardCells, "yardCells", LookMode.Value);
            if (yardCells == null)
            {
                yardCells = new List<IntVec3>();
            }
        }

        // --- UI ---

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            EnsureCacheFresh();
            if (cachedInteriorCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(cachedInteriorCells.ToList(), Color.white);
            }
            if (yardCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(yardCells, Color.green);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (renameIcon == null)
            {
                renameIcon = ContentFinder<Texture2D>.Get("Homestead/UI/Rename");
                yardAddIcon = ContentFinder<Texture2D>.Get("Homestead/UI/YardAdd");
                yardRemoveIcon = ContentFinder<Texture2D>.Get("Homestead/UI/YardRemove");
                expandIcon = ContentFinder<Texture2D>.Get("Homestead/UI/ExpandDoors");
            }

            yield return new Command_Action
            {
                defaultLabel = "Homestead_GizmoRename".Translate(),
                defaultDesc = "Homestead_GizmoRenameDesc".Translate(),
                icon = renameIcon,
                action = () => Find.WindowStack.Add(new Dialog_RenameHouse(this)),
            };

            yield return new Command_Action
            {
                defaultLabel = "Homestead_GizmoAddYard".Translate(),
                defaultDesc = "Homestead_GizmoAddYardDesc".Translate(),
                icon = yardAddIcon,
                action = () => Find.DesignatorManager.Select(new Designator_HouseYard(this, remove: false)),
            };

            if (yardCells.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Homestead_GizmoRemoveYard".Translate(),
                    defaultDesc = "Homestead_GizmoRemoveYardDesc".Translate(),
                    icon = yardRemoveIcon,
                    action = () => Find.DesignatorManager.Select(new Designator_HouseYard(this, remove: true)),
                };
            }

            yield return new Command_Toggle
            {
                defaultLabel = "Homestead_GizmoExpandDoors".Translate(),
                defaultDesc = "Homestead_GizmoExpandDoorsDesc".Translate(),
                icon = expandIcon,
                isActive = () => expandThroughDoors,
                toggleAction = () =>
                {
                    expandThroughDoors = !expandThroughDoors;
                    Manager?.SetDirty();
                },
            };
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();
            sb.AppendLine(HouseName);
            if (!parent.Spawned)
            {
                return sb.ToString().TrimEndNewlines();
            }
            EnsureCacheFresh();

            Room markerRoom = parent.Position.GetRoom(parent.Map);
            if (markerRoom == null || markerRoom.PsychologicallyOutdoors)
            {
                sb.AppendLine("Homestead_InspectOutdoors".Translate());
                return sb.ToString().TrimEndNewlines();
            }

            List<Pawn> owners = Owners;
            if (owners.Count == 0)
            {
                sb.AppendLine("Homestead_InspectNoOwners".Translate());
            }
            else
            {
                sb.AppendLine("Homestead_InspectOwners".Translate() + ": " +
                    owners.Select(p => p.LabelShort).ToCommaList());
            }
            sb.AppendLine("Homestead_InspectRooms".Translate(RoomCount, InteriorCellCount, yardCells.Count));
            sb.AppendLine("Homestead_InspectImpressiveness".Translate(
                AvgImpressiveness.ToString("F0"), ImpressivenessStageLabel));
            sb.AppendLine("Homestead_InspectCleanliness".Translate(AvgCleanliness.ToString("F1")));
            return sb.ToString().TrimEndNewlines();
        }
    }

    public class Dialog_RenameHouse : Dialog_Rename<CompHouseMarker>
    {
        public Dialog_RenameHouse(CompHouseMarker comp) : base(comp)
        {
        }
    }
}
