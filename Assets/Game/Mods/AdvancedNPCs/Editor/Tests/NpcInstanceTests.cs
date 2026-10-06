using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NpcInstanceTests
    {
        static NpcDefinition Def(string id, string gender, string race, string portrait)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.Name = "Name of " + id;
            d.Gender = gender;
            d.Race = race;
            if (portrait != null)
                d.Portraits.Add(portrait);
            d.X = 1f;
            d.Y = 2f;
            d.Z = 3f;
            return d;
        }

        [Test]
        public void ForUnique_CopiesIdentityAndPosition()
        {
            NpcInstance i = NpcInstance.ForUnique(Def("bram", "Male", "", null), "Breton");
            Assert.AreEqual("bram", i.Key);
            Assert.AreEqual("Name of bram", i.Name);
            Assert.AreEqual(StableHash.Of("bram"), i.Seed);
            Assert.IsTrue(i.HasFixedPosition);
            Assert.AreEqual(1f, i.X);
            Assert.AreEqual(2f, i.Y);
            Assert.AreEqual(3f, i.Z);
            Assert.AreEqual(-1, i.CellIndex);
            Assert.IsTrue(i.Persistent);
            Assert.IsNull(i.PortraitName);
        }

        [Test]
        public void ForUnique_KeepsFixedGender()
        {
            Assert.AreEqual("Female", NpcInstance.ForUnique(Def("a", "Female", "", null), "Breton").Gender);
        }

        [Test]
        public void ForUnique_UnsetGender_IsStablePerId()
        {
            string first = NpcInstance.ForUnique(Def("cora", "", "", null), "Breton").Gender;
            string again = NpcInstance.ForUnique(Def("cora", "", "", null), "Breton").Gender;
            Assert.AreEqual(first, again);
            Assert.IsTrue(first == "Male" || first == "Female", first);
        }

        [Test]
        public void ForUnique_RaceFallsBackToDefault()
        {
            Assert.AreEqual("Redguard", NpcInstance.ForUnique(Def("a", "Male", "", null), "Redguard").Race);
            Assert.AreEqual("Nord", NpcInstance.ForUnique(Def("a", "Male", "Nord", null), "Redguard").Race);
        }

        [Test]
        public void ForUnique_UsesFirstPortrait()
        {
            Assert.AreEqual("bram", NpcInstance.ForUnique(Def("a", "Male", "", "bram"), "Breton").PortraitName);
        }

        [Test]
        public void ForUnique_FaceIsStableAndValid()
        {
            NpcInstance a = NpcInstance.ForUnique(Def("bors", "Male", "Nord", null), "Breton");
            NpcInstance b = NpcInstance.ForUnique(Def("bors", "Male", "Nord", null), "Breton");
            Assert.IsTrue(a.FaceOutfit >= 0 && a.FaceOutfit < VanillaFaces.OutfitVariants);
            Assert.IsTrue(a.FaceVariant >= 0 && a.FaceVariant < VanillaFaces.FaceVariants);
            Assert.AreEqual(a.FaceOutfit, b.FaceOutfit);
            Assert.AreEqual(a.FaceVariant, b.FaceVariant);
            Assert.AreEqual(VanillaFaces.FaceRecord("Nord", "Male", a.FaceOutfit, a.FaceVariant), a.FaceRecord());
        }
    }
}
