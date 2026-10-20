using UnityEngine;
using DaggerfallWorkshop;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Conversations between ANPCs (spec conversations): after greeting another opted-in ANPC this one may start one.
    /// The one who started drives it (turns, talk sounds and sheets, facing); either side can end it.
    /// </summary>
    public class NpcChat : MonoBehaviour
    {
        const float TurnDegreesPerSecond = 540f;
        /// <summary>Seconds between the greeting and the first line.</summary>
        const float StartDelay = 0.3f;
        /// <summary>Longest wait for a greet sheet to finish before the first line.</summary>
        const float MaxGreetWait = 3f;

        /// <summary>When each pair last talked (not saved).</summary>
        public static readonly ChatMemory Memory = new ChatMemory();
        /// <summary>Conversations started (self-test).</summary>
        public static int Started;

        ConversationSettings settings = new ConversationSettings();
        NpcBrain brain;
        NpcSprite sprite;
        NpcVoice voice;
        ChatSession session;
        NpcChat partner;
        bool initiator;
        float delay;
        float greetWait;

        public void Init(ConversationSettings s)
        {
            settings = s ?? new ConversationSettings();
        }

        void Awake()
        {
            brain = GetComponent<NpcBrain>();
            sprite = GetComponent<NpcSprite>();
            voice = GetComponent<NpcVoice>();
        }

        public ConversationSettings Settings
        {
            get { return settings; }
        }

        public NpcBrain Brain
        {
            get { return brain; }
        }

        public ChatSession Session
        {
            get { return session; }
        }

        public NpcChat Partner
        {
            get { return partner; }
        }

        /// <summary>This one started the conversation and drives it.</summary>
        public bool IsInitiator
        {
            get { return initiator; }
        }

        public bool Talking
        {
            get { return session != null && !session.Over; }
        }

        /// <summary>Why this ANPC cannot join a conversation now, or null.</summary>
        public string WhyNot()
        {
            if (!isActiveAndEnabled || brain == null)
                return "not here";      // an unloaded town's people (never active: Awake has not run)
            if (sprite == null)
                sprite = GetComponent<NpcSprite>();
            if (voice == null)
                voice = GetComponent<NpcVoice>();
            if (brain.PackageOnlyCombatInterrupts) return "animation package only interrupts for combat";
            return ChatRules.Ineligible(settings.Enabled, Talking, brain.ChatSide, voice != null && voice.CustomVoiceOn,
                voice != null ? voice.TalkClips : 0);
        }

        /// <summary>Both could start talking now (the socialize package's partner test): TryStart would succeed.</summary>
        public bool CanTalkWith(NpcChat other)
        {
            return other != null && other != this && other.brain != null && WhyNot() == null && other.WhyNot() == null &&
                GreetRules.SameSide(brain.ChatSide, other.brain.ChatSide) && Memory.CanStart(ChatRules.PairKey(brain.Id, other.brain.Id), NowHours());
        }

        /// <summary>The greeter greeted another ANPC: maybe start a conversation (this one's chance, the pair's wait).</summary>
        public void OnGreeted(NpcBrain other)
        {
            NpcChat o = other != null ? other.GetComponent<NpcChat>() : null;
            if (o == null || !settings.Enabled || !SoundRules.Rolls(settings.Chance, Random.value))
                return;
            if (!Memory.CanStart(ChatRules.PairKey(brain.Id, other.Id), NowHours()))
                return;
            TryStart(o, false);
        }

        /// <summary>
        /// Starts a conversation with other, this one speaking first. Forced (anpc_chat) ignores the pair's 2-hour wait,
        /// not who may talk. Null, or why not.
        /// </summary>
        public string TryStart(NpcChat other, bool forced)
        {
            if (other == null || other == this)
                return "no partner";
            string why = WhyNot();
            if (why != null)
                return brain.DisplayName + ": " + why;
            why = other.WhyNot();
            if (why != null)
            {
                NpcBrain otherBrain = other.brain != null ? other.brain : other.GetComponent<NpcBrain>();
                return (otherBrain != null ? otherBrain.DisplayName : other.name) + ": " + why;
            }
            if (!GreetRules.SameSide(brain.ChatSide, other.brain.ChatSide))
                return "not on the same side (one is an enemy of yours, the other is not)";
            string pair = ChatRules.PairKey(brain.Id, other.brain.Id);
            if (!forced && !Memory.CanStart(pair, NowHours()))
                return "they talked less than 2 game hours ago";
            Memory.Started(pair, NowHours());
            session = new ChatSession(ChatRules.Lines(Random.value));
            other.session = session;
            partner = other;
            other.partner = this;
            initiator = true;
            other.initiator = false;
            delay = StartDelay;
            greetWait = MaxGreetWait;
            Started++;
            AdvancedNpcsMod.Log(brain.Id + " and " + other.brain.Id + ": conversation of " + session.Lines + " lines.");
            return null;
        }

        void Update()
        {
            if (!initiator || session == null)
                return;
            if (session.Over)
            {
                End(false);
                return;
            }
            if (ShouldEnd())
            {
                End(true);
                return;
            }
            float dt = Time.deltaTime;
            Face(this, partner, dt);
            Face(partner, this, dt);
            brain.HoldStill(0.5f);
            partner.brain.HoldStill(0.5f);
            if (delay > 0f)
            {
                // A greeting (wave or hello) finishes before the first line.
                bool greeting = Greeting(this) || Greeting(partner);
                greetWait -= dt;
                if (!greeting || greetWait <= 0f)
                    delay -= dt;
                return;
            }
            session.Tick(dt, Random.value);
            if (session.NeedsLine)
            {
                NpcChat speaker = session.Speaker == 0 ? this : partner;
                NpcChat listener = speaker == this ? partner : this;
                if (listener.sprite != null)
                    listener.sprite.StopTalk();
                float seconds = speaker.voice != null ? speaker.voice.PlayTalk() : 0f;
                session.BeginLine(seconds);
                if (seconds > 0f && speaker.sprite != null)
                    speaker.sprite.Talk(seconds);
            }
            if (session.Over)
                End(false);
        }

        /// <summary>Spec conversations §3: a partner gone, either busy (an enemy spotted someone) or without its talk sounds, or apart.</summary>
        bool ShouldEnd()
        {
            if (partner == null || !partner.isActiveAndEnabled || partner.session != session)
                return true;
            if (!StillFit(this) || !StillFit(partner) || !GreetRules.SameSide(brain.ChatSide, partner.brain.ChatSide))
                return true;
            Vector3 d = partner.transform.position - transform.position;
            d.y = 0;
            return d.magnitude > ChatRules.MaxApart;
        }

        static bool Greeting(NpcChat c)
        {
            return (c.sprite != null && c.sprite.Greeting) || (c.voice != null && c.voice.PlayingGreet);
        }

        static bool StillFit(NpcChat c)
        {
            return c.brain != null && !c.brain.PackageOnlyCombatInterrupts && c.voice != null && c.voice.CustomVoiceOn && c.voice.TalkClips > 0;
        }

        static void Face(NpcChat who, NpcChat toward, float dt)
        {
            Vector3 to = toward.transform.position - who.transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.0001f)
                who.transform.rotation = Quaternion.RotateTowards(who.transform.rotation, Quaternion.LookRotation(to), TurnDegreesPerSecond * dt);
        }

        /// <summary>Ends the conversation for both; cutShort also stops a line still playing.</summary>
        public void End(bool cutShort)
        {
            NpcChat other = partner;
            Leave(cutShort);
            if (other != null && other.partner == this)
                other.Leave(cutShort);
        }

        void Leave(bool cutShort)
        {
            if (session != null)
                session.End();
            NpcGreeter greeter = GetComponent<NpcGreeter>();
            if (greeter != null && partner != null)
                greeter.Met(partner.brain);     // no wave at the one it just talked with
            if (cutShort && voice != null)
                voice.StopTalk();
            if (sprite != null)
                sprite.StopTalk();
            session = null;
            partner = null;
            initiator = false;
        }

        void OnDisable()
        {
            // Unloaded or discarded mid-conversation: the partner must not wait for lines that never come.
            if (session != null)
                End(true);
        }

        static double NowHours()
        {
            return DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() / 3600.0;
        }
    }
}
