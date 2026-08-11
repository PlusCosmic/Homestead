using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Homestead
{
    public class HomesteadGameComponent : GameComponent
    {
        private bool colonyEstablished;
        private Dictionary<Pawn, int> homelessSince = new Dictionary<Pawn, int>();
        private HashSet<int> notifiedCouples = new HashSet<int>();
        private int lastSettlerTick = -999999;

        private List<Pawn> scribeKeys;
        private List<int> scribeValues;

        private const int HomelessScanInterval = 2000;
        private const int CoupleScanInterval = 12000;
        private const int SettlerScanInterval = 2500;
        private const float SettlerMtbDays = 12f;
        private const float SettlerCooldownDays = 6f;

        public HomesteadGameComponent(Game game)
        {
        }

        public static HomesteadGameComponent Instance => Current.Game?.GetComponent<HomesteadGameComponent>();

        public bool ColonyEstablished =>
            colonyEstablished || Find.TickManager.TicksGame > HomesteadSettings.EstablishedDays * GenDate.TicksPerDay;

        public void Notify_HouseExists() => colonyEstablished = true;

        // Ticks this pawn has been without a house, or -1 if housed/untracked.
        public int HomelessTicks(Pawn pawn)
        {
            return homelessSince.TryGetValue(pawn, out int since) ? Find.TickManager.TicksGame - since : -1;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int tick = Find.TickManager.TicksGame;
            if (tick % HomelessScanInterval == 117)
            {
                HomelessScan();
            }
            if (tick % CoupleScanInterval == 233)
            {
                CoupleScan();
            }
            if (tick % SettlerScanInterval == 331)
            {
                SettlerScan();
            }
        }

        // An empty furnished house draws the occasional settler: MTB 12 days per
        // empty house, only while the storyteller still wants more colonists.
        private void SettlerScan()
        {
            if (!(HomesteadMod.Settings?.enableSettlerAttraction ?? true) || !ColonyEstablished)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick - lastSettlerTick < SettlerCooldownDays * GenDate.TicksPerDay)
            {
                return;
            }
            if (StorytellerUtilityPopulation.PopulationIntent <= 0f)
            {
                return;
            }
            var emptyHouses = new List<CompHouseMarker>();
            foreach (Map map in Find.Maps)
            {
                HouseManager manager = map.GetComponent<HouseManager>();
                if (manager == null)
                {
                    continue;
                }
                foreach (CompHouseMarker marker in manager.Markers)
                {
                    if (marker.IsFunctional && marker.Owners.Count == 0 && HasFreeBed(marker))
                    {
                        emptyHouses.Add(marker);
                    }
                }
            }
            if (emptyHouses.Count == 0
                || !Rand.MTBEventOccurs(SettlerMtbDays / emptyHouses.Count, GenDate.TicksPerDay, SettlerScanInterval))
            {
                return;
            }
            CompHouseMarker house = emptyHouses.RandomElement();
            Map targetMap = house.parent.Map;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => targetMap.reachability.CanReachColony(c) && !c.Fogged(targetMap),
                    targetMap, CellFinder.EdgeRoadChance_Neutral, out IntVec3 entry))
            {
                return;
            }
            Pawn settler = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Villager, Faction.OfPlayer, PawnGenerationContext.NonPlayer, targetMap.Tile,
                forceGenerateNewPawn: true, colonistRelationChanceFactor: 20f));
            GenSpawn.Spawn(settler, entry, targetMap);
            house.Assignable?.TryAssignPawn(settler);
            lastSettlerTick = tick;

            TaggedString text = "Homestead_LetterSettlerText".Translate(
                settler.Named("PAWN"), house.HouseName).AdjustedFor(settler);
            TaggedString title = "Homestead_LetterSettlerLabel".Translate(settler.Named("PAWN")).AdjustedFor(settler);
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, ref title, settler);
            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent,
                new LookTargets(new List<Thing> { settler, house.parent }));
        }

        private static bool HasFreeBed(CompHouseMarker house)
        {
            foreach (Room room in house.cachedRooms)
            {
                if (room == null || room.Dereferenced)
                {
                    continue;
                }
                foreach (Building_Bed bed in room.ContainedBeds)
                {
                    if (bed.def.building.bed_humanlike && !bed.ForPrisoners && !bed.Medical
                        && bed.Faction == Faction.OfPlayer && bed.OwnersForReading.Count == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void HomelessScan()
        {
            var current = new HashSet<Pawn>();
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.ageTracker.AgeBiologicalYears < 13 || pawn.IsQuestLodger())
                    {
                        continue;
                    }
                    if (HouseManager.HouseOf(pawn) == null)
                    {
                        current.Add(pawn);
                        if (!homelessSince.ContainsKey(pawn))
                        {
                            homelessSince[pawn] = Find.TickManager.TicksGame;
                        }
                    }
                }
            }
            homelessSince.RemoveAll(kv => !current.Contains(kv.Key));
        }

        private void CoupleScan()
        {
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    CompHouseMarker house = HouseManager.HouseOf(pawn);
                    if (house == null)
                    {
                        continue;
                    }
                    foreach (DirectPawnRelation rel in pawn.relations.DirectRelations)
                    {
                        if (rel.def != PawnRelationDefOf.Spouse && rel.def != PawnRelationDefOf.Fiance)
                        {
                            continue;
                        }
                        Pawn partner = rel.otherPawn;
                        if (partner == null || partner.Dead || !partner.IsFreeColonist || pawn.thingIDNumber >= partner.thingIDNumber)
                        {
                            continue;
                        }
                        CompHouseMarker partnerHouse = HouseManager.HouseOf(partner);
                        if (partnerHouse == null || partnerHouse == house)
                        {
                            continue;
                        }
                        // Skipped, not marked notified: the letter can still fire
                        // later if they marry (or their ideoligion changes).
                        if (!CompAssignableToPawn_House.IdeoAllowsCohabitation(pawn, partner))
                        {
                            continue;
                        }
                        int key = Gen.HashCombineInt(pawn.thingIDNumber, partner.thingIDNumber);
                        if (!notifiedCouples.Add(key))
                        {
                            continue;
                        }
                        Find.LetterStack.ReceiveLetter(
                            "Homestead_LetterMergeLabel".Translate(),
                            "Homestead_LetterMergeText".Translate(pawn.LabelShort, partner.LabelShort),
                            LetterDefOf.NeutralEvent,
                            new LookTargets(new[] { pawn, partner }));
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref colonyEstablished, "colonyEstablished");
            Scribe_Values.Look(ref lastSettlerTick, "lastSettlerTick", -999999);
            Scribe_Collections.Look(ref homelessSince, "homelessSince",
                LookMode.Reference, LookMode.Value, ref scribeKeys, ref scribeValues);
            Scribe_Collections.Look(ref notifiedCouples, "notifiedCouples", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                homelessSince ??= new Dictionary<Pawn, int>();
                notifiedCouples ??= new HashSet<int>();
                homelessSince.RemoveAll(kv => kv.Key == null);
            }
        }
    }
}
