using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Homestead
{
    public class JoyGiver_VisitFriendHome : JoyGiver
    {
        private const int MinOpinion = 20;

        public override Job TryGiveJob(Pawn pawn)
        {
            var settings = HomesteadMod.Settings;
            if (settings != null && !settings.enableGuestVisits)
            {
                return null;
            }
            if (!pawn.Spawned || pawn.Map == null)
            {
                return null;
            }
            HouseManager manager = pawn.Map.GetComponent<HouseManager>();
            if (manager == null)
            {
                return null;
            }
            CompHouseMarker ownHouse = HouseManager.HouseOf(pawn);

            var candidates = new List<(Pawn friend, CompHouseMarker house, float weight)>();
            foreach (CompHouseMarker house in manager.Markers)
            {
                if (!house.IsFunctional || house == ownHouse)
                {
                    continue;
                }
                foreach (Pawn friend in house.Owners)
                {
                    if (friend == pawn || friend.Dead || !friend.Spawned || friend.Map != pawn.Map)
                    {
                        continue;
                    }
                    int opinion = pawn.relations.OpinionOf(friend);
                    if (opinion < MinOpinion)
                    {
                        continue;
                    }
                    float weight = opinion;
                    if (friend.Awake() && house.ContainsCell(friend.Position))
                    {
                        weight *= 3f;
                    }
                    candidates.Add((friend, house, weight));
                }
            }
            if (candidates.Count == 0)
            {
                return null;
            }
            var pick = candidates.RandomElementByWeight(c => c.weight);
            if (!TryFindVisitCell(pawn, pick.house, out IntVec3 cell))
            {
                return null;
            }
            return JobMaker.MakeJob(def.jobDef, cell, pick.friend, pick.house.parent);
        }

        private static bool TryFindVisitCell(Pawn pawn, CompHouseMarker house, out IntVec3 cell)
        {
            house.EnsureCacheFresh();
            IEnumerable<IntVec3> cells = house.cachedInteriorCells
                .Where(c => c.Standable(pawn.Map)
                    && pawn.CanReach(c, PathEndMode.OnCell, Danger.None)
                    && c.GetDoor(pawn.Map) == null);
            return cells.TryRandomElement(out cell);
        }
    }

    public class JobDriver_VisitFriendHome : JobDriver
    {
        private const TargetIndex CellInd = TargetIndex.A;
        private const TargetIndex FriendInd = TargetIndex.B;
        private const TargetIndex MarkerInd = TargetIndex.C;

        private bool arrived;

        private Pawn Friend => (Pawn)job.GetTarget(FriendInd).Thing;
        private Thing Marker => job.GetTarget(MarkerInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Marker == null || Marker.Destroyed || !Marker.Spawned);

            yield return Toils_Goto.GotoCell(CellInd, PathEndMode.OnCell);

            Toil visit = Toils_General.Wait(job.def.joyDuration);
            visit.initAction = () => arrived = true;
            visit.socialMode = RandomSocialMode.SuperActive;
            visit.tickAction = () =>
            {
                Pawn friend = Friend;
                if (friend != null && friend.Spawned && friend.Position.InHorDistOf(pawn.Position, 8f))
                {
                    pawn.rotationTracker.FaceTarget(friend);
                }
                JoyUtility.JoyTickCheckEnd(pawn, 1);
            };
            visit.AddFinishAction(() =>
            {
                if (!arrived)
                {
                    return;
                }
                CompHouseMarker house = Marker?.TryGetComp<CompHouseMarker>();
                if (house == null)
                {
                    return;
                }
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(HomesteadDefOf.Homestead_VisitedFriend, Friend);
                foreach (Pawn owner in house.Owners)
                {
                    if (owner.Spawned && owner.Map == pawn.Map && owner.Awake() && house.ContainsCell(owner.Position))
                    {
                        owner.needs?.mood?.thoughts?.memories?.TryGainMemory(HomesteadDefOf.Homestead_HadGuestOver, pawn);
                    }
                }
            });
            yield return visit;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arrived, "arrived");
        }
    }
}
