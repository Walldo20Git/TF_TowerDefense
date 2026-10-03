using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe de la Oleada 4: Mezcla Final (GDD 5). Combina las mecánicas anteriores en tres fases:
    /// - Fase 1 (100–66 %): zonas de ruido de Distorsión.
    /// - Fase 2 (66–33 %): añade el pulso que silencia torres de Feedback.
    /// - Fase 3 (33–0 %): acelera y además suelta esbirros como la Reina Glitch.
    /// </summary>
    public class BossFinalMix : BossBase
    {
        [Header("Fase 1: Distorsión")]
        [SerializeField] private GameObject noiseZonePrefab;

        [Header("Fase 2: Feedback")]
        [Tooltip("Radio del pulso de silencio, en unidades de campo.")]
        [SerializeField] private float silenceRadius = 0.55f;
        [SerializeField] private float silenceDuration = 9f;

        [Header("Fase 3: Reina Glitch")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private int minionsPerPulse = 2;
        [Tooltip("Multiplicador de velocidad al entrar en la fase 3.")]
        [SerializeField] private float enragedSpeedMultiplier = 1.3f;

        private int currentPhase = 1;

        public int CurrentPhase => currentPhase;

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount);
            if (isDead) return;

            float healthPct = currentHealth / maxHealth;
            int phase = healthPct <= 0.33f ? 3 : (healthPct <= 0.66f ? 2 : 1);

            while (currentPhase < phase)
            {
                currentPhase++;
                EnterPhase(currentPhase);
            }
        }

        private void EnterPhase(int phase)
        {
            if (phase == 3)
            {
                moveSpeed *= enragedSpeedMultiplier;
                abilityInterval *= 0.75f;
            }

            PulseEffect.Spawn(abilityVfxPrefab, transform.position, 1.5f);
            GameMessages.Show($"¡Mezcla Final entra en la fase {phase}!", 2.5f);
        }

        protected override void ExecuteBossAbility()
        {
            DeployNoiseZone(noiseZonePrefab);

            if (currentPhase >= 2) SilenceTowers(silenceRadius, silenceDuration);
            if (currentPhase >= 3) SpawnMinions(minionPrefab, minionsPerPulse, 0.12f);
        }
    }
}
