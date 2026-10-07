using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SpriteSetTests
    {
        const string Label = "wench/sprites/sprites.json";
        const string Wench =
            "{ \"pixelsPerUnit\": 64.6018, \"cellHeight\": 256, \"groundY\": 32, \"anchorX\": \"centre\", \"fps\": 8, " +
            "\"directions\": [\"front\", \"front_right\", \"right\", \"back_right\", \"back\", \"back_left\", \"left\", \"front_left\"], " +
            "\"animations\": { \"death\": { \"cellWidth\": 344, \"frames\": 6 }, \"idle_1\": { \"cellWidth\": 104, \"frames\": 10 } } }";

        static string With(string replace, string by)
        {
            Assert.IsTrue(Wench.Contains(replace), replace);
            return Wench.Replace(replace, by);
        }

        [Test]
        public void RenderScriptOutput_Parses()
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, Wench);
            Assert.IsNull(r.Error, r.Error);
            SpriteSet s = r.Set;
            Assert.AreEqual(64.6018f, s.PixelsPerUnit, 0.0001f);
            Assert.AreEqual(256, s.CellHeight);
            Assert.AreEqual(32, s.GroundY);
            Assert.AreEqual(8, s.Fps);
            Assert.AreEqual(0, s.DeathStaticGroundY);
            Assert.AreEqual(2, s.Animations.Count);
            Assert.AreEqual(344, s.Animations["death"].CellWidth);
            Assert.AreEqual(6, s.Animations["death"].Frames);
            Assert.AreEqual("idle_1", s.Animations["idle_1"].Name);
        }

        [Test]
        public void DeathStaticGround_IsOptional()
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, With("\"fps\": 8,", "\"fps\": 8, \"deathStatic\": { \"groundY\": 12 },"));
            Assert.IsNull(r.Error, r.Error);
            Assert.AreEqual(12, r.Set.DeathStaticGroundY);
        }

        const string IdleSheet = "\"idle_1\": { \"cellWidth\": 104, \"frames\": 10 }";

        static string WithAttack(string attack)
        {
            return With(IdleSheet, IdleSheet + ", \"attack_1\": " + attack);
        }

        [Test]
        public void ActionFrame_IsOptional()
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, WithAttack("{ \"cellWidth\": 272, \"frames\": 5 }"));
            Assert.IsNull(r.Error, r.Error);
            Assert.AreEqual(-1, r.Set.Animations["attack_1"].ActionFrame);
            Assert.AreEqual(-1, r.Set.Animations["death"].ActionFrame);
        }

        [Test]
        public void ActionFrame_CountsFromOne()
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, WithAttack("{ \"cellWidth\": 272, \"frames\": 5, \"actionFrame\": 3 }"));
            Assert.IsNull(r.Error, r.Error);
            Assert.AreEqual(2, r.Set.Animations["attack_1"].ActionFrame);
        }

        [TestCase("0")]
        [TestCase("6")]
        [TestCase("2.5")]
        [TestCase("\"3\"")]
        public void ActionFrame_OutOfRange_IsRejected(string value)
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, WithAttack("{ \"cellWidth\": 272, \"frames\": 5, \"actionFrame\": " + value + " }"));
            Assert.AreEqual(Label + ": animations.attack_1.actionFrame: must be a whole number from 1 to 5", r.Error);
        }

        [Test]
        public void ActionFrame_OnlyOnAttackSheets()
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, With("\"cellWidth\": 344, \"frames\": 6", "\"cellWidth\": 344, \"frames\": 6, \"actionFrame\": 2"));
            Assert.AreEqual(Label + ": animations.death.actionFrame: only attack sheets have an action frame", r.Error);
        }

        [TestCase("\"pixelsPerUnit\": 64.6018", "\"pixelsPerUnit\": 0", "pixelsPerUnit: must be a number above 0")]
        [TestCase("\"cellHeight\": 256", "\"cellHeight\": -1", "cellHeight: must be a whole number above 0")]
        [TestCase("\"groundY\": 32", "\"groundY\": 300", "groundY: must be a whole number from 0 to 255")]
        [TestCase("\"fps\": 8", "\"fps\": 0", "fps: must be a number above 0")]
        [TestCase("\"anchorX\": \"centre\"", "\"anchorX\": \"left\"", "anchorX: only \"centre\" is supported")]
        [TestCase("\"front_right\", \"right\"", "\"right\", \"front_right\"", "directions: must be front, front_right, right, back_right, back, back_left, left, front_left")]
        [TestCase("\"cellWidth\": 344, \"frames\": 6", "\"cellWidth\": 344, \"frames\": 0", "animations.death.frames: must be a whole number above 0")]
        [TestCase("\"cellWidth\": 344, \"frames\": 6", "\"cellWidth\": 0, \"frames\": 6", "animations.death.cellWidth: must be a whole number above 0")]
        public void BadValues_AreRejected(string replace, string by, string expected)
        {
            Assert.AreEqual(Label + ": " + expected, SpriteSetParser.Parse(Label, With(replace, by)).Error);
        }

        [Test]
        public void NoAnimations_IsRejected()
        {
            SpriteSetResult r = SpriteSetParser.Parse(Label, "{ \"pixelsPerUnit\": 64, \"cellHeight\": 256, \"groundY\": 32, \"fps\": 8, \"animations\": {} }");
            Assert.AreEqual(Label + ": animations: needs at least one sheet", r.Error);
        }

        [Test]
        public void InvalidJson_IsRejected()
        {
            StringAssert.StartsWith(Label + ": file: invalid JSON", SpriteSetParser.Parse(Label, "{ nope").Error);
        }

        [Test]
        public void WrongSheetSize_IsRejected()
        {
            SpriteSet s = SpriteSetParser.Parse(Label, Wench).Set;
            Assert.IsNull(SpriteSetParser.CheckSheetSize("wench/sprites", s.Animations["death"], s.CellHeight, 2064, 2048));
            Assert.AreEqual("wench/sprites/death.png: file: must be 2064 x 2048 (6 frames of 344 x 256, 8 directions), got 2000 x 2048",
                SpriteSetParser.CheckSheetSize("wench/sprites", s.Animations["death"], s.CellHeight, 2000, 2048));
        }
    }
}
