using System;
using HarmonyLib;
using Verse;

namespace Homestead
{
    // Soft integration with Dubs Bad Hygiene. Full and Lite are separate packages
    // built from the same source, so both expose these types; resolving them by
    // name needs no assembly reference and no-ops cleanly when neither is loaded.
    [StaticConstructorOnStartup]
    public static class DbhCompat
    {
        private static readonly Type toiletType;      // all toilets and latrines
        private static readonly Type bathType;
        private static readonly Type showerType;
        private static readonly Type basinType;
        private static readonly Type washBucketType;
        private static readonly Type waterTroughType; // subclasses washbucket, but is for animals

        static DbhCompat()
        {
            toiletType = AccessTools.TypeByName("DubsBadHygiene.Building_BaseToilet");
            bathType = AccessTools.TypeByName("DubsBadHygiene.Building_bath");
            showerType = AccessTools.TypeByName("DubsBadHygiene.Building_shower");
            basinType = AccessTools.TypeByName("DubsBadHygiene.Building_basin");
            washBucketType = AccessTools.TypeByName("DubsBadHygiene.Building_washbucket");
            waterTroughType = AccessTools.TypeByName("DubsBadHygiene.Building_WaterTrough");
            if (Active)
            {
                Log.Message("[Homestead] Dubs Bad Hygiene detected; houses want bathrooms.");
            }
        }

        public static bool Active => toiletType != null;

        public static bool IsToilet(Thing t) => toiletType != null && toiletType.IsInstanceOfType(t);

        public static bool IsWashingFixture(Thing t)
        {
            if (t == null || (waterTroughType != null && waterTroughType.IsInstanceOfType(t)))
            {
                return false;
            }
            return (bathType != null && bathType.IsInstanceOfType(t))
                || (showerType != null && showerType.IsInstanceOfType(t))
                || (basinType != null && basinType.IsInstanceOfType(t))
                || (washBucketType != null && washBucketType.IsInstanceOfType(t));
        }
    }
}
