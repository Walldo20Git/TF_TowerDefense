using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe de la Oleada 3: Reina Glitch (GDD 5).
    /// Al recibir daño se divide: suelta glitches pequeños al cruzar el 75 %, 50 % y 25 % de vida,
    /// y un último grupo al ser derrotada.
    /// </summary>
    public class BossGlitchQueen : BossBase
    {
        [Header("Habilidad: División")]
        [Tooltip("Prefab del glitch menor que suelta al dividirse.")]
        [SerializeField] private GameObject minionPrefab;

        [Tooltip("Esbirros por cada umbral de vida.")]
        [SerializeField] private int minionsPerThreshold = 2;

        [Tooltip("Esbirros que suelta al morir.")]
        [SerializeField] private int minionsOnDeath = 3;

        private static readonly float[] Thresholds = { 0.75f, 0.5f, 0.25f };
        private int nextThreshold;

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount);
            if (isDead) return;

            // Un golpe fuerte puede cruzar varios umbrales a la vez
            float healthPct = currentHealth / maxHealth;
            while (nextThreshold < Thresholds.Length && healthPct <= Thresholds[nextThreshold])
            {
                nextThreshold++;
                Split(minionsPerThreshold);
            }
        }

        private void Split(int count)
        {
            SpawnMinions(minionPrefab, count, 0.1f);
            PulseEffect.Spawn(abilityVfxPrefab, transform.position, 1.2f);
            GameMessages.Show("¡La Reina Glitch se divide!", 2f);
        }

        protected override void OnRemovedFromField(bool defeated)
        {
            if (defeated) SpawnMinions(minionPrefab, minionsOnDeath, 0.12f);
            base.OnRemovedFromField(defeated);
        }
    }
}
