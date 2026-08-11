using RimWorld;
using Verse;

namespace Homestead
{
    public class ThoughtWorker_HouseImpressiveness : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.IsFreeColonist)
            {
                return ThoughtState.Inactive;
            }
            CompHouseMarker house = HouseManager.HouseOf(p);
            if (house == null || !house.IsFunctional || p.MapHeld != house.parent.Map)
            {
                return ThoughtState.Inactive;
            }
            int stage = house.ImpressivenessStageIndex;
            if (stage < 0 || stage >= def.stages.Count || def.stages[stage] == null)
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(stage);
        }
    }

    public class ThoughtWorker_FilthyHouse : ThoughtWorker
    {
        public const float Threshold = -1.2f;
        public const float SevereThreshold = -3.5f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.IsFreeColonist)
            {
                return ThoughtState.Inactive;
            }
            CompHouseMarker house = HouseManager.HouseOf(p);
            if (house == null || !house.IsFunctional || p.MapHeld != house.parent.Map)
            {
                return ThoughtState.Inactive;
            }
            float cleanliness = house.AvgCleanliness;
            if (cleanliness < SevereThreshold)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            if (cleanliness < Threshold)
            {
                return ThoughtState.ActiveAtStage(0);
            }
            return ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_Homeless : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var settings = HomesteadMod.Settings;
            if (settings != null && !settings.enableHomelessThought)
            {
                return ThoughtState.Inactive;
            }
            if (!p.IsFreeNonSlaveColonist || p.IsQuestLodger() || p.ageTracker.AgeBiologicalYears < 13)
            {
                return ThoughtState.Inactive;
            }
            HomesteadGameComponent comp = HomesteadGameComponent.Instance;
            if (comp == null || !comp.ColonyEstablished)
            {
                return ThoughtState.Inactive;
            }
            if (HouseManager.HouseOf(p) != null)
            {
                return ThoughtState.Inactive;
            }
            int homelessTicks = comp.HomelessTicks(p);
            if (homelessTicks < HomesteadSettings.HomelessGraceDays * GenDate.TicksPerDay)
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(
                homelessTicks > HomesteadSettings.HomelessSevereDays * GenDate.TicksPerDay ? 1 : 0);
        }
    }
}
