using System;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// How a dead ANPC's body is drawn (spec corpse-sound settings). The death sheet plays from the side it is seen
    /// from; the body then rests on the last frame of its front or back row, as a billboard like DFU's own corpses and
    /// death_static.png, unless the "Directional corpses" setting keeps it turning with the view. No Unity or DFU types.
    /// </summary>
    public static class CorpseRules
    {
        public static readonly int FrontRow = Array.IndexOf(SpriteSetParser.Directions, "front");
        public static readonly int BackRow = Array.IndexOf(SpriteSetParser.Directions, "back");

        /// <summary>The row a body rests on, front or back at random (roll in [0, 1]).</summary>
        public static int RestRow(double roll01)
        {
            return roll01 < 0.5 ? FrontRow : BackRow;
        }

        /// <summary>Whether the body is one picture that always faces the view.</summary>
        public static bool Billboard(bool showingStatic, bool finished, bool directionalCorpses)
        {
            return showingStatic || (finished && !directionalCorpses);
        }

        /// <summary>The death sheet row to show: the resting row for a billboard, else the row the body is seen from.</summary>
        public static int Row(bool billboard, int restRow, int viewRow)
        {
            return billboard ? restRow : viewRow;
        }

        /// <summary>The death sheet frame: playing at fps, then held on the last one (at once when finished).</summary>
        public static int Frame(bool finished, float time, int fps, int frames)
        {
            if (finished)
                return frames - 1;
            return Math.Min((int)(time * fps), frames - 1);
        }
    }
}
