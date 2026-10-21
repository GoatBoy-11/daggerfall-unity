using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public enum Bravery
    {
        Coward,
        Normal,
        Brave,
        Prudent,
    }

    public enum NpcKind
    {
        Unique,
        Generic,
    }

    /// <summary>A validated ANPC definition (one folder's npc.json) used at runtime.</summary>
    public class NpcDefinition
    {
        public string Id;
        public NpcKind Kind = NpcKind.Unique;
        public string Name = "";
        public string Region;
        public string Place;
        public float X;
        public float Y;
        public float Z;
        public string BaseClass;
        public string Gender;
        /// <summary>"Breton", "Redguard", "Nord", "DarkElf", or "" for the region's people.</summary>
        public string Race = "";
        /// <summary>Normalised portrait names (PortraitNames); a unique ANPC has 0 or 1.</summary>
        public readonly List<string> Portraits = new List<string>();
        /// <summary>Generic templates: names to pick from (empty: fixed Name or generated).</summary>
        public readonly List<string> Names = new List<string>();
        /// <summary>Name list for generated names (normalized); "" = default_&lt;race&gt;.</summary>
        public string NameList = "";
        /// <summary>World height of a custom sprite's standing pose; 0 = the vanilla class sprite's height.</summary>
        public float SpriteHeight;
        /// <summary>Optional recruitment price; -1 disables companion commands.</summary>
        public int RecruitCost = -1;
        /// <summary>"no_companion_slot": may join without consuming a limited party slot.</summary>
        public bool NoCompanionSlot;
        /// <summary>Vertical-only custom sprite scale, anchored at the feet; 1 keeps the original proportions.</summary>
        public float SpriteHeightScale = 1f;
        /// <summary>"spriteScale": shrinks or grows the whole custom sprite evenly (0.2-2); 1 keeps the default size.</summary>
        public float SpriteScale = 1f;
        /// <summary>Generic templates only: where and how many (null for unique ANPCs).</summary>
        public GenericSpawn Spawn;
        public Bravery Bravery;
        public int FleeHealthPercent;
        /// <summary>"fleeChance": % chance a Normal ANPC flees once below half of FleeHealthPercent (0-100).</summary>
        public int FleeChance = 30;
        /// <summary>"aggroRange": metres a calm Brave ANPC looks for enemies (1-50).</summary>
        public float AggroRange = AggroRules.DefaultRange;
        public float CalmDownMinHours;
        public float CalmDownMaxHours;
        public bool CrimeOnAttack;
        public bool Guard;
        /// <summary>"attitude": "hostile": an enemy (attacks the player on sight, no crime to fight).</summary>
        public bool Hostile;
        /// <summary>"hostileHours" [from, to): hostile only in those hours; -1 = always.</summary>
        public int HostileFrom = -1;
        public int HostileTo = -1;
        /// <summary>BaseClass is a DFU creature (Creatures), not a human class.</summary>
        public bool IsCreature;
        public float WanderRadius;
        /// <summary>Optional daily routine; empty keeps the original AI.</summary>
        public Dictionary<string, NpcVariableValue> Variables = new Dictionary<string, NpcVariableValue>();
        public List<AiScheduleEntry> Schedule = new List<AiScheduleEntry>();
        /// <summary>Optional spellcasting override; null keeps vanilla class behaviour.</summary>
        public NpcMagicDefinition Magic;
        /// <summary>"combat": multiplier on DFU's rolled max health (1 = unchanged).</summary>
        public float HealthScale = 1f;
        /// <summary>"combat": multiplier on a creature's melee damage ranges (1 = unchanged).</summary>
        public float DamageScale = 1f;
        /// <summary>"combat": melee attack rate; 1.3 waits 1/1.3 as long between swings (1 = unchanged).</summary>
        public float AttackSpeed = 1f;
        public bool HasCombatTuning { get { return HealthScale != 1f || DamageScale != 1f || AttackSpeed != 1f; } }
        /// <summary>"combat": shoot DFU's bow at a distant target (human classes), or throw (creatures); off by default.</summary>
        public bool RangedAtDistance;
        /// <summary>"combat": the sheet played for a bow shot or throw; null picks attack_range* sheets, else attack*.</summary>
        public string RangedAnimation;
        /// <summary>"combat.projectileDamage": damage of a ranged hit, min to max (0 = DFU's own).</summary>
        public int ProjectileDamageMin;
        public int ProjectileDamageMax;
        /// <summary>"combat": running speed as a multiple of walk speed (1-2).</summary>
        public float RunSpeed = GaitRules.DefaultRunSpeed;
        /// <summary>"combat": false = never runs (chases and flees at walk speed).</summary>
        public bool CanRun = true;
        /// <summary>npc.json "sounds": chances, step distance and pitch of the folder's custom sounds (spec sounds §C).</summary>
        public SoundSettings Sounds = new SoundSettings();
        /// <summary>npc.json "conversation": talks with other ANPCs it greets (spec conversations §1); off by default.</summary>
        public ConversationSettings Conversation = new ConversationSettings();
        /// <summary>Dialogue type ids (files in _Dialogue), in the order given in npc.json.</summary>
        public readonly List<string> Dialogue = new List<string>();
        /// <summary>The folder's own dialogue.json, or null.</summary>
        public DialogueFile OwnDialogue;
        /// <summary>Folder name under ANPCs (equals Id).</summary>
        public string Folder = "";
        /// <summary>Path used in messages, e.g. "bram/npc.json".</summary>
        public string SourceFile;
        /// <summary>"replaces": vanilla flat NPCs this generic ANPC stands in for, or null.</summary>
        public ReplaceSettings Replace;
        /// <summary>npc.json "animal": temperament, senses and hunting (spec animal-ai); null for people.</summary>
        public AnimalSettings Animal;
        /// <summary>npc.json "disease": what its melee hits may pass on to the player; null = none.</summary>
        public DiseaseSettings Disease;
    }
}
