using RimWorld;
using Verse;

namespace Homestead
{
    [DefOf]
    public static class HomesteadDefOf
    {
        public static JobDef Homestead_VisitFriendHome;
        public static ThoughtDef Homestead_StrangerInMyHouse;
        public static ThoughtDef Homestead_RelaxedAtHome;
        public static ThoughtDef Homestead_VisitedFriend;
        public static ThoughtDef Homestead_HadGuestOver;
        public static ThoughtDef Homestead_HouseImpressiveness;
        public static ThoughtDef Homestead_FilthyHouse;
        public static ThingDef Homestead_HouseMarker;

        static HomesteadDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HomesteadDefOf));
        }
    }
}
