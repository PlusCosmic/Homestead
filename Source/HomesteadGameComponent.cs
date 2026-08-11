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

        private List<Pawn> scribeKeys;
        private List<int> scribeValues;

        private const int HomelessScanInterval = 2000;
        private const int CoupleScanInterval = 12000;

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
