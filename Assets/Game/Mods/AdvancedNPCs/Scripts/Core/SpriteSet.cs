using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>One sheet of a sprite set: 8 direction rows of Frames cells, each CellWidth wide.</summary>
    public class SpriteAnimation
    {
        public string Name;
        public int CellWidth;
        public int Frames;
        /// <summary>Attack sheets: the frame (from 0) on which the blow lands; -1 keeps DFU's own timing.</summary>
        public int ActionFrame = -1;
    }

    /// <summary>A sprite set's sprites.json (spec 1b §5.1), as written by the Blender render script.</summary>
    public class SpriteSet
    {
        public float PixelsPerUnit;
        public int CellHeight;
        /// <summary>Feet row, counted from the bottom of a frame.</summary>
        public int GroundY;
        public int Fps;
        /// <summary>Ground row of death_static.png, counted from its bottom edge.</summary>
        public int DeathStaticGroundY;
        public readonly Dictionary<string, SpriteAnimation> Animations = new Dictionary<string, SpriteAnimation>(StringComparer.Ordinal);
    }

    public class SpriteSetResult
    {
        public SpriteSet Set;
        public string Error;
    }

    public static class SpriteSetParser
    {
        /// <summary>Row order of every sheet (spec 1b §5.1).</summary>
        public static readonly string[] Directions = { "front", "front_right", "right", "back_right", "back", "back_left", "left", "front_left" };

        public static SpriteSetResult Parse(string label, string json)
        {
            SpriteSetResult r = new SpriteSetResult();
            object root;
            try
            {
                root = Json.Parse(json);
            }
            catch (JsonException e)
            {
                r.Error = label + ": file: invalid JSON (" + e.Message + ")";
                return r;
            }
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
            {
                r.Error = label + ": file: invalid JSON (top level must be an object)";
                return r;
            }
            SpriteSet s = new SpriteSet();
            r.Error = Read(label, o, s);
            if (r.Error == null)
                r.Set = s;
            return r;
        }

        /// <summary>Null if the sheet image has exactly the size sprites.json describes, else the error line.</summary>
        public static string CheckSheetSize(string folderLabel, SpriteAnimation a, int cellHeight, int width, int height)
        {
            int w = a.Frames * a.CellWidth;
            int h = Directions.Length * cellHeight;
            if (width == w && height == h)
                return null;
            return folderLabel + "/" + a.Name + ".png: file: must be " + w + " x " + h + " (" + a.Frames + " frames of " +
                   a.CellWidth + " x " + cellHeight + ", " + Directions.Length + " directions), got " + width + " x " + height;
        }

        static string Read(string label, Dictionary<string, object> o, SpriteSet s)
        {
            double ppu;
            if (FieldReader.Number(o, "pixelsPerUnit", 0, out ppu) != null || !(ppu > 0))
                return label + ": pixelsPerUnit: must be a number above 0";
            s.PixelsPerUnit = (float)ppu;

            int cellHeight;
            if (!WholeAbove(o, "cellHeight", 0, out cellHeight))
                return label + ": cellHeight: must be a whole number above 0";
            s.CellHeight = cellHeight;

            double ground;
            if (FieldReader.Number(o, "groundY", 0, out ground) != null || ground != Math.Floor(ground) || ground < 0 || ground >= cellHeight)
                return label + ": groundY: must be a whole number from 0 to " + (cellHeight - 1);
            s.GroundY = (int)ground;

            double fps;
            if (FieldReader.Number(o, "fps", 0, out fps) != null || !(fps > 0))
                return label + ": fps: must be a number above 0";
            s.Fps = (int)Math.Max(1, Math.Round(fps));

            string anchor;
            if (FieldReader.Text(o, "anchorX", "centre", out anchor) != null || anchor != "centre")
                return label + ": anchorX: only \"centre\" is supported";

            List<string> dirs;
            if (FieldReader.Texts(o, "directions", out dirs) != null || (dirs != null && string.Join(",", dirs.ToArray()) != string.Join(",", Directions)))
                return label + ": directions: must be " + string.Join(", ", Directions);

            Dictionary<string, object> deathStatic;
            if (FieldReader.Object(o, "deathStatic", out deathStatic) != null)
                return label + ": deathStatic: must be an object";
            if (deathStatic != null)
            {
                double dg;
                if (FieldReader.Number(deathStatic, "groundY", 0, out dg) != null || dg != Math.Floor(dg) || dg < 0)
                    return label + ": deathStatic.groundY: must be a whole number of 0 or more";
                s.DeathStaticGroundY = (int)dg;
            }

            Dictionary<string, object> anims;
            if (FieldReader.Object(o, "animations", out anims) != null || anims == null)
                return label + ": animations: must be an object of sheets";
            foreach (KeyValuePair<string, object> kv in anims)
            {
                Dictionary<string, object> a = kv.Value as Dictionary<string, object>;
                string field = "animations." + kv.Key;
                if (a == null)
                    return label + ": " + field + ": must be an object with cellWidth and frames";
                SpriteAnimation anim = new SpriteAnimation();
                anim.Name = kv.Key;
                if (!WholeAbove(a, "cellWidth", 0, out anim.CellWidth))
                    return label + ": " + field + ".cellWidth: must be a whole number above 0";
                if (!WholeAbove(a, "frames", 0, out anim.Frames))
                    return label + ": " + field + ".frames: must be a whole number above 0";
                if (a.ContainsKey("actionFrame"))
                {
                    if (SpriteStates.StateOf(kv.Key) != SpriteStates.Attack)
                        return label + ": " + field + ".actionFrame: only attack sheets have an action frame";
                    double action;
                    if (FieldReader.Number(a, "actionFrame", 0, out action) != null || action != Math.Floor(action) || action < 1 || action > anim.Frames)
                        return label + ": " + field + ".actionFrame: must be a whole number from 1 to " + anim.Frames;
                    anim.ActionFrame = (int)action - 1;     // written from 1, like the sheet's columns
                }
                s.Animations[kv.Key] = anim;
            }
            if (s.Animations.Count == 0)
                return label + ": animations: needs at least one sheet";
            return null;
        }

        static bool WholeAbove(Dictionary<string, object> o, string key, int min, out int value)
        {
            double d;
            value = 0;
            if (FieldReader.Number(o, key, min, out d) != null || d != Math.Floor(d) || d <= min)
                return false;
            value = (int)d;
            return true;
        }
    }
}
