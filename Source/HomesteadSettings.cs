using Verse;

namespace Homestead
{
    public class HomesteadSettings : ModSettings
    {
        public bool enableHomelessThought = true;
        public bool enableGuestVisits = true;
        public bool enableAvoidance = true;
        public bool replaceBedroomThoughts = true;
        public float moodScale = 1f;
        public int claimDepth = 3;
        public int maxRoomCells = 225;

        public const float HomelessGraceDays = 7f;
        public const float HomelessSevereDays = 22f;
        public const int EstablishedDays = 60;
        public const int MaxYardCells = 500;
        public const float MaxYardDistance = 40f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableHomelessThought, "enableHomelessThought", true);
            Scribe_Values.Look(ref enableGuestVisits, "enableGuestVisits", true);
            Scribe_Values.Look(ref enableAvoidance, "enableAvoidance", true);
            Scribe_Values.Look(ref replaceBedroomThoughts, "replaceBedroomThoughts", true);
            Scribe_Values.Look(ref moodScale, "moodScale", 1f);
            Scribe_Values.Look(ref claimDepth, "claimDepth", 3);
            Scribe_Values.Look(ref maxRoomCells, "maxRoomCells", 225);
        }
    }
}
