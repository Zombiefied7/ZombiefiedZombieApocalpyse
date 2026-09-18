using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Zombiefied
{
    /// <summary>
    /// Assigns collision-safe short hashes to defs created after RimWorld's normal def-loading pass.
    /// RimWorld's ShortHashGiver keeps the required method private, so runtime def generators must
    /// bridge to it when they add defs after GiveAllShortHashes has already completed.
    /// </summary>
    internal static class InjectedDefHasher
    {
        private static readonly Dictionary<Type, HashSet<ushort>> TakenHashesPerDefType;
        private static readonly Action<Def, Type, HashSet<ushort>> GiveShortHash;

        static InjectedDefHasher()
        {
            var takenHashesField = AccessTools.Field(typeof(ShortHashGiver), "takenHashesPerDeftype");
            var giveShortHashMethod = AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash");

            if (takenHashesField == null || giveShortHashMethod == null)
            {
                throw new MissingMemberException(
                    "Zombiefied could not access RimWorld's ShortHashGiver internals required for runtime-generated defs.");
            }

            TakenHashesPerDefType = takenHashesField.GetValue(null) as Dictionary<Type, HashSet<ushort>>;
            if (TakenHashesPerDefType == null)
            {
                throw new InvalidOperationException(
                    "Zombiefied could not read RimWorld's short-hash registry for runtime-generated defs.");
            }

            GiveShortHash = (Action<Def, Type, HashSet<ushort>>)giveShortHashMethod.CreateDelegate(
                typeof(Action<Def, Type, HashSet<ushort>>));
        }

        internal static void GiveShortHashToDef(Def def, Type defType)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            if (defType == null)
            {
                throw new ArgumentNullException(nameof(defType));
            }

            if (def.shortHash != 0)
            {
                return;
            }

            if (!TakenHashesPerDefType.TryGetValue(defType, out HashSet<ushort> takenHashes))
            {
                takenHashes = new HashSet<ushort>();
                TakenHashesPerDefType.Add(defType, takenHashes);
            }

            GiveShortHash(def, defType, takenHashes);
        }
    }
}
