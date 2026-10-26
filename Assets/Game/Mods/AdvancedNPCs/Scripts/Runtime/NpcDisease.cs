using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.MagicAndEffects;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// npc.json "disease" (spec animal-ai, Disease): a melee hit on the player may pass on one of the listed diseases
    /// through DFU's own InflictDisease (level 1 immunity and the saving throw apply). A hit is a drop in the player's
    /// health while this ANPC is fighting the player, close, in its attack state.
    /// </summary>
    public class NpcDisease : MonoBehaviour
    {
        const float Reach = 3.5f;

        /// <summary>DFU's infection; the self-test swaps it so its character is never really infected.</summary>
        public static System.Action<DaggerfallEntity, DaggerfallEntity, Diseases[]> Inflict = FormulaHelper.InflictDisease;

        DiseaseSettings settings;
        NpcBrain brain;
        DaggerfallEntityBehaviour behaviour;
        MobileUnit mobile;
        int lastPlayerHealth = -1;

        /// <summary>Melee hits on the player seen, and diseases passed on (anpc_info, self-test).</summary>
        public int Hits { get; private set; }
        public int Infections { get; private set; }

        public void Init(DiseaseSettings diseaseSettings)
        {
            settings = diseaseSettings;
        }

        void Start()
        {
            brain = GetComponent<NpcBrain>();
            behaviour = GetComponent<DaggerfallEntityBehaviour>();
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            mobile = enemy != null ? enemy.MobileUnit : null;
        }

        void LateUpdate()
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (player == null || brain == null)
                return;
            int health = player.CurrentHealth;
            if (lastPlayerHealth >= 0 && health < lastPlayerHealth && brain.CurrentMode == NpcMode.Fighting &&
                brain.Threat == GameManager.Instance.PlayerEntityBehaviour && mobile != null && mobile.EnemyState == MobileStates.PrimaryAttack &&
                Vector3.Distance(transform.position, GameManager.Instance.PlayerObject.transform.position) <= Reach)
                Hit(Random.Range(0, 100));
            lastPlayerHealth = health;
        }

        /// <summary>One melee hit on the player with a roll 0-99: below the chance passes on a disease. True if it did.</summary>
        public bool Hit(int roll)
        {
            Hits++;
            if (settings == null || settings.Diseases.Count == 0 || roll >= settings.Chance || behaviour == null)
                return false;
            Diseases[] list = new Diseases[settings.Diseases.Count];
            for (int i = 0; i < list.Length; i++)
                list[i] = (Diseases)settings.Diseases[i];
            Inflict(behaviour.Entity, GameManager.Instance.PlayerEntity, list);
            Infections++;
            AdvancedNpcsMod.Log((brain != null ? brain.Id : name) + ": a hit may pass on " + string.Join("/", System.Array.ConvertAll(list, delegate (Diseases x) { return x.ToString(); })) + ".");
            return true;
        }
    }
}
