using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;

namespace AdvancedNPCs
{
    /// <summary>Native flight, visuals and collisions; permission is checked again before native effects are assigned.</summary>
    [DefaultExecutionOrder(-300)]
    public class CompanionSpellMissile : MonoBehaviour
    {
        NpcCompanionCombat combat;
        DaggerfallEntityBehaviour selected;
        DaggerfallMissile missile;
        EntityEffectBundle payload;
        bool delivered;
        public static int Impacts { get; private set; }
        public static bool Launch(NpcBrain brain, EntityEffectManager effects, EffectBundleSettings spell, DaggerfallEntityBehaviour target)
        {
            DaggerfallMissile native = effects.InstantiateSpellMissile(spell.ElementType);
            if (native == null) return false;
            native.Payload = new EntityEffectBundle(spell, brain.EntityBehaviour);
            var guard = native.gameObject.AddComponent<CompanionSpellMissile>();
            guard.combat = brain.CompanionCombat;
            guard.selected = target;
            guard.missile = native;
            guard.payload = native.Payload;
            native.transform.SetParent(brain.transform.parent, true);
            DaggerfallMissile.OnComplete += guard.Complete;
            return true;
        }

        void Update()
        {
            if (missile == null) return;
            missile.enabled = !GameManager.IsGamePaused;
            if (GameManager.IsGamePaused) return;
            if (!delivered && (combat == null || !combat.AllowsShotHit(selected)))
            {
                // Immediately disarm; Destroy runs at the end of this frame.
                missile.Payload = null;
                Destroy(gameObject);
            }
        }

        void Complete()
        {
            if (delivered || missile == null || GameManager.IsGamePaused) return;
            // This native event is static. Only a missile with its own collision targets is ready.
            DaggerfallEntityBehaviour[] targets = missile.Targets;
            if (targets.Length == 0) return;
            delivered = true;
            // DFU raises OnComplete before its automatic AssignPayloadToTargets. Disarm first,
            // then let the real recipient's effect manager perform saves, resistance and damage.
            missile.Payload = null;
            if (combat == null || !combat.AllowsShotHit(selected)) return;
            for (int i = 0; i < targets.Length; i++)
            {
                DaggerfallEntityBehaviour target = targets[i];
                if (System.Array.IndexOf(targets, target) != i) continue;
                if (!combat.AllowsSpellHit(target, selected)) continue;
                EntityEffectManager manager = target.GetComponent<EntityEffectManager>();
                if (manager == null) continue;
                manager.AssignBundle(payload, AssignBundleFlags.ShowNonPlayerFailures);
                Impacts++;
            }
        }

        void OnDisable()
        {
            DaggerfallMissile.OnComplete -= Complete;
            if (missile != null) missile.Payload = null;
        }
    }
}
