using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Greetings (spec run/greet/turret §5): a calm ANPC with a greet sheet now and then stops, turns to a player or
    /// ANPC it likes within 4 m and in sight, and plays the sheet once. Once per encounter, 25 %, 2 game hours per target.
    /// Greeting another ANPC may start a conversation (NpcChat); while talking it greets no one.
    /// </summary>
    public class NpcGreeter : MonoBehaviour
    {
        const float CheckInterval = 0.5f;
        const float TurnDegreesPerSecond = 540f;

        /// <summary>Self-test: forces the reaction band toward the player (null = DFU's).</summary>
        public static string ReactionOverride;
        /// <summary>Self-test: forces the chance (negative = GreetRules.Chance).</summary>
        public static float ChanceOverride = -1f;

        /// <summary>
        /// No greet sheet or sound: it greets only other ANPCs, without turning, so they may start a conversation
        /// (spec conversations §1).
        /// </summary>
        public bool Silent;

        readonly GreetMemory memory = new GreetMemory();
        NpcBrain brain;
        NpcSprite sprite;
        NpcVoice voice;
        NpcChat chat;
        float nextCheck;
        Transform facingTarget;
        float facingLeft;

        /// <summary>Greetings given (self-test).</summary>
        public int Greetings
        {
            get { return memory.Count; }
        }

        void Start()
        {
            brain = GetComponent<NpcBrain>();
            sprite = GetComponent<NpcSprite>();
            voice = GetComponent<NpcVoice>();
            chat = GetComponent<NpcChat>();
            nextCheck = Time.time + Random.Range(0f, CheckInterval);
        }

        void Update()
        {
            // Calm, or an enemy with no one to fight yet (it greets only other such enemies).
            if (brain == null || (sprite == null && voice == null) || brain.ChatSide == GreetRules.Busy)
            {
                facingTarget = null;
                return;
            }
            if (chat != null && chat.Talking)
            {
                facingTarget = null;    // busy talking: greets no one (spec conversations §3)
                return;
            }
            if (facingTarget != null && facingLeft > 0f)
            {
                facingLeft -= Time.deltaTime;
                Vector3 to = facingTarget.position - transform.position;
                to.y = 0;
                if (to.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnDegreesPerSecond * Time.deltaTime);
            }
            if (Time.time < nextCheck)
                return;
            GameObject playerObject = GameManager.Instance.PlayerObject;
            if (playerObject != null && Vector3.Distance(playerObject.transform.position, transform.position) > NpcLod.GreetDistance)
            {
                nextCheck = Time.time + CheckInterval;
                memory.LeaveAll();      // not watching: whoever comes back is a new encounter
                return;     // no one sees a greeting this far away (spec opinion-lod §B)
            }
            nextCheck = Time.time + CheckInterval;
            CheckCandidates();
        }

        void CheckCandidates()
        {
            int selfSide = brain.ChatSide;
            bool selfCalm = selfSide == GreetRules.Peaceful;
            double now = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() / 3600.0;
            double chance = ChanceOverride >= 0f ? ChanceOverride : GreetRules.Chance;

            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            if (player != null && !Silent)
            {
                bool near = Near(player.transform) && brain.CanSee(player);
                bool likes = near && GreetRules.LikesPlayer(selfCalm, PlayerReactionBand(brain.Opinion));
                if (memory.Consider(GreetRules.PlayerKey, near, likes, now, Random.value, chance))
                {
                    Greet(player.transform);
                    return;
                }
            }
            foreach (NpcBrain other in NpcBrain.All())
            {
                if (other == brain || other.CurrentMode == NpcMode.Dead || other.EntityBehaviour == null)
                    continue;
                bool near = Near(other.transform) && brain.CanSee(other.EntityBehaviour);
                bool likes = near && GreetRules.SameSide(selfSide, other.ChatSide);
                if (memory.Consider(other.Id, near, likes, now, Random.value, chance))
                {
                    Greet(other.transform);
                    if (chat != null)
                        chat.OnGreeted(other);
                    return;
                }
            }
        }

        /// <summary>A conversation with this ANPC just ended: they met, so no greeting until it leaves and comes back.</summary>
        public void Met(NpcBrain other)
        {
            if (other != null)
                memory.Met(other.Id);
        }

        bool Near(Transform target)
        {
            Vector3 d = target.position - transform.position;
            d.y = 0;
            return d.magnitude <= GreetRules.Distance;
        }

        void Greet(Transform target)
        {
            if (Silent)
                return;     // nothing to show or hear
            facingTarget = target;
            facingLeft = 0.6f;
            if (sprite != null && sprite.PlayGreet())
                brain.HoldStill(sprite.GreetSeconds + 0.3f);
            if (voice != null)
                voice.Play(SoundPools.Greet);
        }

        /// <summary>
        /// DFU's reaction to the player for a townsperson here (the region's people) plus this ANPC's own opinion,
        /// as a dialogue band.
        /// </summary>
        internal static string PlayerReactionBand(int opinion)
        {
            if (ReactionOverride != null)
                return ReactionOverride;
            try
            {
                FactionFile.FactionData people;
                PlayerEntity p = GameManager.Instance.PlayerEntity;
                if (TalkManager.Instance == null ||
                    !p.FactionData.GetFactionData(GameManager.Instance.PlayerGPS.GetPeopleOfCurrentRegion(), out people))
                    return OpinionRules.Band(0, opinion);
                return OpinionRules.Band(TalkManager.Instance.GetReactionToPlayer(people), opinion);
            }
            catch (System.Exception)
            {
                return OpinionRules.Band(0, opinion);
            }
        }
    }
}
