using System.Linq;
using RimWorld;
using Verse;

namespace Homestead
{
    public class ThoughtWorker_HouseImpressiveness : ThoughtWorker
    {
        // Vanilla's top band starts at 240; a whole house averaging 1.5x that is
        // beyond anything a single bedroom can express, and earns the extra stage.
        public const float PalatialThreshold = 360f;

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
            int vanillaBands = RoomStatDefOf.Impressiveness.scoreStages.Count;
            if (stage == vanillaBands - 1 && house.AvgImpressiveness >= PalatialThreshold)
            {
                stage = vanillaBands;
            }
            if (stage < 0 || stage >= def.stages.Count || def.stages[stage] == null)
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(stage);
        }
    }

    public class ThoughtWorker_FamilyUnderOneRoof : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.IsFreeColonist)
            {
                return ThoughtState.Inactive;
            }
            CompHouseMarker house = HouseManager.HouseOf(p);
            if (house == null || !house.IsFunctional)
            {
                return ThoughtState.Inactive;
            }
            bool partnerHome = false;
            bool childOrParentHome = false;
            foreach (Pawn other in house.Owners)
            {
                if (other == null || other == p || other.Dead)
                {
                    continue;
                }
                if (LovePartnerRelationUtility.LovePartnerRelationExists(p, other))
                {
                    partnerHome = true;
                }
                else if (p.relations.Children.Contains(other) || other.relations.Children.Contains(p))
                {
                    childOrParentHome = true;
                }
            }
            if (partnerHome && childOrParentHome)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            if (partnerHome || childOrParentHome)
            {
                return ThoughtState.ActiveAtStage(0);
            }
            return ThoughtState.Inactive;
        }
    }

    // Only active when Dubs Bad Hygiene (full or Lite) is loaded.
    public class ThoughtWorker_HouseBathroom : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!DbhCompat.Active || !(HomesteadMod.Settings?.enableBathroomNeeds ?? true))
            {
                return ThoughtState.Inactive;
            }
            if (!p.IsFreeColonist)
            {
                return ThoughtState.Inactive;
            }
            CompHouseMarker house = HouseManager.HouseOf(p);
            if (house == null || !house.IsFunctional || p.MapHeld != house.parent.Map)
            {
                return ThoughtState.Inactive;
            }
            bool toilet = house.HasToilet;
            bool washing = house.HasWashing;
            if (!toilet && !washing)
            {
                return ThoughtState.ActiveAtStage(0);
            }
            if (!toilet)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            if (!washing)
            {
                return ThoughtState.ActiveAtStage(2);
            }
            return ThoughtState.ActiveAtStage(3);
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
