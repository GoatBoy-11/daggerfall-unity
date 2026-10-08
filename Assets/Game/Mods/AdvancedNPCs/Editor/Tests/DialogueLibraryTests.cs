using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DialogueLibraryTests
    {
        static DialogueFile File(string name, string json)
        {
            DialogueParseResult r = DialogueParser.Parse(name + ".json", json);
            Assert.IsNotNull(r.File, string.Join("\n", r.Messages.ToArray()));
            r.File.Name = name;
            return r.File;
        }

        static NpcDefinition Npc(string id, params string[] types)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.SourceFile = id + "/npc.json";
            d.Dialogue.AddRange(types);
            return d;
        }

        static List<string> Captions(ComposedDialogue c)
        {
            List<string> captions = new List<string>();
            foreach (DialogueTopic t in c.Topics)
                captions.Add(t.Caption);
            return captions;
        }

        DialogueLibrary library;

        [SetUp]
        public void SetUp()
        {
            library = new DialogueLibrary();
            library.Add(File("gossip", "{ \"greetings\": [\"Psst.\"], \"topics\": [ { \"caption\": \"Rumours\", \"answers\": [\"G\"] }, { \"caption\": \"Weather\", \"answers\": [\"Grey\"] } ] }"));
            library.Add(File("tavern_wench", "{ \"greetings\": [\"Welcome!\"], \"topics\": [ { \"caption\": \"Ale\", \"answers\": [\"A\"] }, { \"caption\": \"rumours\", \"answers\": [\"W\"] } ] }"));
        }

        [Test]
        public void Compose_TypesInOrder_SameIdReplacesInPlace()
        {
            ComposedDialogue c = library.Compose(Npc("w", "gossip", "tavern_wench"));
            CollectionAssert.AreEqual(new[] { "rumours", "Weather", "Ale" }, Captions(c));
            Assert.AreEqual("W", c.Topics[0].Answers[0].Text);
            CollectionAssert.AreEqual(new[] { "gossip", "tavern_wench" }, c.Types);
        }

        [Test]
        public void Compose_FolderFileLast_AndGreetingsConcatenated()
        {
            NpcDefinition d = Npc("w", "tavern_wench");
            d.OwnDialogue = File("w", "{ \"greetings\": [\"Me!\"], \"topics\": [ { \"caption\": \"Ale\", \"answers\": [\"Mine\"] }, { \"caption\": \"Me\", \"answers\": [\"x\"] } ] }");
            ComposedDialogue c = library.Compose(d);
            CollectionAssert.AreEqual(new[] { "Ale", "rumours", "Me" }, Captions(c));
            Assert.AreEqual("Mine", c.Topics[0].Answers[0].Text);
            Assert.AreEqual(2, c.Greetings.Count);
            Assert.AreEqual("Me!", c.Greetings[1].Text);
        }

        [Test]
        public void Compose_UnknownTypeSkipped_NoDialogueIsEmpty()
        {
            ComposedDialogue c = library.Compose(Npc("w", "nope", "gossip"));
            CollectionAssert.AreEqual(new[] { "Rumours", "Weather" }, Captions(c));
            Assert.IsTrue(library.Compose(Npc("plain")).IsEmpty);
            Assert.IsFalse(c.IsEmpty);
        }

        [Test]
        public void Check_ReportsMissingTypesOnce()
        {
            NpcDefinition d = Npc("wench", "tavern_wnch", "tavern_wnch");
            List<string> messages = library.Check(new[] { d });
            CollectionAssert.AreEqual(new[] { "wench/npc.json: dialogue \"tavern_wnch\": no such file in _Dialogue, ignored" }, messages);
        }

        [Test]
        public void Check_AskedMustNameAComposedTopic_AcrossFiles()
        {
            library.Add(File("followups", "{ \"topics\": [ " +
                "{ \"caption\": \"Sailor\", \"when\": { \"asked\": \"Rumours\" }, \"answers\": [\"S\"] }, " +
                "{ \"caption\": \"Ghost\", \"when\": { \"asked\": \"ghosts\" }, \"answers\": [ { \"when\": { \"notAsked\": \"spooks\" }, \"text\": \"G\" } ] } ] }"));
            List<string> messages = library.Check(new[] { Npc("w", "gossip", "followups") });
            CollectionAssert.AreEqual(new[]
            {
                "followups.json: topic \"Ghost\": asked/notAsked: no topic \"ghosts\" for w (check its caption or id)",
                "followups.json: topic \"Ghost\": asked/notAsked: no topic \"spooks\" for w (check its caption or id)",
            }, messages);
        }
    }
}
