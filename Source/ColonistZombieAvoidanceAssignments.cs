using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class ColonistZombieAvoidanceAssignments : GameComponent
    {
        private List<string> enabledPawnIds = new List<string>();

        public ColonistZombieAvoidanceAssignments(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref enabledPawnIds, "zombiefiedZombieAvoidancePawnIds", LookMode.Value);

            if (enabledPawnIds == null)
            {
                enabledPawnIds = new List<string>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                HashSet<string> uniqueIds = new HashSet<string>();
                for (int i = enabledPawnIds.Count - 1; i >= 0; i--)
                {
                    string pawnId = enabledPawnIds[i];
                    if (string.IsNullOrEmpty(pawnId) || !uniqueIds.Add(pawnId))
                    {
                        enabledPawnIds.RemoveAt(i);
                    }
                }
            }
        }

        public bool IsEnabled(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return enabledPawnIds.Contains(pawn.GetUniqueLoadID());
        }

        public void SetEnabled(Pawn pawn, bool enabled)
        {
            if (pawn == null)
            {
                return;
            }

            string pawnId = pawn.GetUniqueLoadID();
            if (enabled)
            {
                if (!enabledPawnIds.Contains(pawnId))
                {
                    enabledPawnIds.Add(pawnId);
                }
            }
            else
            {
                enabledPawnIds.Remove(pawnId);
            }
        }

        public static bool IsEnabledFor(Pawn pawn)
        {
            if (Current.Game == null)
            {
                return false;
            }

            ColonistZombieAvoidanceAssignments assignments = Current.Game.GetComponent<ColonistZombieAvoidanceAssignments>();
            return assignments != null && assignments.IsEnabled(pawn);
        }

        public static void SetEnabledFor(Pawn pawn, bool enabled)
        {
            if (Current.Game == null)
            {
                return;
            }

            ColonistZombieAvoidanceAssignments assignments = Current.Game.GetComponent<ColonistZombieAvoidanceAssignments>();
            if (assignments != null)
            {
                assignments.SetEnabled(pawn, enabled);
            }
        }
    }

    public static class ZombieAvoidanceResponseUtility
    {
        private const string NormalPathingLabel = "Normal pathing";
        private const string AvoidZombiesLabel = "Avoid zombies";
        private static readonly Color IconColor = new Color(0.84f, 0.84f, 0.84f);

        public static void DrawResponseButton(Rect rect, Pawn pawn, bool paintable)
        {
            if (!ZombieAvoidanceUtility.CanUsePlayerAvoidanceAssignment(pawn))
            {
                return;
            }

            Func<Pawn, bool> getPayload = GetResponse;
            Func<Pawn, IEnumerable<Widgets.DropdownMenuElement<bool>>> menuGenerator = GenerateMenu;
            bool enabled = ColonistZombieAvoidanceAssignments.IsEnabledFor(pawn);
            Texture2D icon = enabled
                ? HostilityResponseMode.Flee.GetIcon()
                : HostilityResponseMode.Ignore.GetIcon();

            Widgets.Dropdown<Pawn, bool>(
                rect,
                pawn,
                IconColor,
                getPayload,
                menuGenerator,
                null,
                icon,
                null,
                null,
                null,
                paintable,
                4f);

            TooltipHandler.TipRegion(
                rect,
                "Controls zombie-aware pathing for this colonist.\n\nCurrent mode: "
                + GetLabel(enabled)
                + "\n\nAvoid zombies makes ordinary movement prefer routes farther from zombies. "
                + "It does not prevent drafted or manually ordered attacks on zombies.");
        }

        private static bool GetResponse(Pawn pawn)
        {
            return ColonistZombieAvoidanceAssignments.IsEnabledFor(pawn);
        }

        private static IEnumerable<Widgets.DropdownMenuElement<bool>> GenerateMenu(Pawn pawn)
        {
            yield return new Widgets.DropdownMenuElement<bool>
            {
                option = new FloatMenuOption(
                    NormalPathingLabel,
                    delegate
                    {
                        ColonistZombieAvoidanceAssignments.SetEnabledFor(pawn, false);
                    },
                    HostilityResponseMode.Ignore.GetIcon(),
                    Color.white),
                payload = false
            };

            yield return new Widgets.DropdownMenuElement<bool>
            {
                option = new FloatMenuOption(
                    AvoidZombiesLabel,
                    delegate
                    {
                        ColonistZombieAvoidanceAssignments.SetEnabledFor(pawn, true);
                    },
                    HostilityResponseMode.Flee.GetIcon(),
                    Color.white),
                payload = true
            };
        }

        private static string GetLabel(bool enabled)
        {
            return enabled ? AvoidZombiesLabel : NormalPathingLabel;
        }
    }

    public class PawnColumnWorker_ZombieAvoidance : PawnColumnWorker
    {
        private const int TopPadding = 3;
        private const int Width = 24;

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            ZombieAvoidanceResponseUtility.DrawResponseButton(rect, pawn, true);
        }

        public override int GetMinCellHeight(Pawn pawn)
        {
            return Mathf.Max(base.GetMinCellHeight(pawn), Mathf.CeilToInt(Width) + TopPadding);
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), Width);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return GetValueToCompare(a).CompareTo(GetValueToCompare(b));
        }

        private int GetValueToCompare(Pawn pawn)
        {
            if (!ZombieAvoidanceUtility.CanUsePlayerAvoidanceAssignment(pawn))
            {
                return -2147483648;
            }

            return ColonistZombieAvoidanceAssignments.IsEnabledFor(pawn) ? 1 : 0;
        }
    }
}
