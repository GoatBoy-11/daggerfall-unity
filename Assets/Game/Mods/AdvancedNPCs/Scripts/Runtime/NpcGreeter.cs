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
    /// </summary>
    public class NpcGreeter : MonoBehaviour
    {
        const float CheckInterval = 0.5f;
        const float TurnDegreesPerSecond = 540f;

        /// <summary>Self-test: forces the reaction band toward the player (null = DFU's).</summary>
        public static string ReactionOverride;
        /// <summary>Self-test: forces the chance (negative = GreetRules.Chance).</summary>
        public static float ChanceOverride = -1f;

        readonly GreetMemory memory = new GreetMemory();
        NpcBrain brain;
        NpcSprite sprite;
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
            nextCheck = Time.time + Random.Range(0f, CheckInterval);
        }

        void Update()
        {
            if (brain == null || sprite == null || brain.CurrentMode != NpcMode.Calm)
            {
                facingTarget = null;
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
            nextCheck = Time.time + CheckInterval;
            CheckCandidates();
        }

        void CheckCandidates()
        {
            bool selfCalm = brain.CurrentMode == NpcMode.Calm && !brain.State.hostile && !brain.IsEnemy;
            double now = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() / 3600.0;
            double chance = ChanceOverride >= 0f ? ChanceOverride : GreetRules.Chance;

            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            if (player != null)
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
                bool likes = near && GreetRules.SameSide(selfCalm, brain.IsEnemy,
                    other.CurrentMode == NpcMode.Calm && !other.State.hostile, other.IsEnemy);
                if (memory.Consider(other.Id, near, likes, now, Random.value, chance))
                {
                    Greet(other.transform);
                    return;
                }
            }
        }

        bool Near(Transform target)
        {
            Vector3 d = target.position - transform.position;
            d.y = 0;
            return d.magnitude <= GreetRules.Distance;
        }

        void Greet(Transform target)
        {
            facingTarget = target;
            facingLeft = 0.6f;
            if (sprite.PlayGreet())
                brain.HoldStill(sprite.GreetSeconds + 0.3f);
        }

        /// <summary>
        /// DFU's reaction to the player for a townsperson here (the region's people) plus this ANPC's own opinion,
        /// as a dialogue band.
        /// </summary>
        static string PlayerReactionBand(int opinion)
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
