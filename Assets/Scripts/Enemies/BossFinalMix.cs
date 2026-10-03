using UnityEngine;
using ConcertDefense.Towers;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe final de la Oleada 4: Mezcla Final (GDD 5):
    /// - Jefe multifase con la mayor cantidad de vida del juego (1500 HP).
    /// - Fase 1 (100% - 66% HP): Despliega zonas de ruido de Distorsión.
    /// - Fase 2 (66% - 33% HP): Incorpora pulsos de acople que silencian torres de Feedback.
    /// - Fase 3 (33% - 0% HP): Estado de sobrecarga/furia que combina todas las habilidades y genera esbirros glitch.
    /// </summary>
    public class BossFinalMix : BossBase
    {
        [Header("Fase 1: Mecánica Distorsión")]
        [Tooltip("Prefab de la zona de ruido que frena proyectiles.")]
        [SerializeField] private GameObject noiseZonePrefab;

        [Header("Fase 2: Mecánica Feedback")]
        [Tooltip("Radio del pulso de silenciado de torres.")]
        [SerializeField] private float silenceRadius = 2.2f;

        [Tooltip("Duración en segundos del silenciado a las torres.")]
        [SerializeField] private float silenceDuration = 5.0f;

        [Tooltip("Efecto de pulso expansivo de interferencia.")]
        [SerializeField] private GameObject feedbackPulseVfxPrefab;

        [Header("Fase 3: Mecánica Reina Glitch (Sobrecarga)")]
        [Tooltip("Prefab del esbirro menor generado durante la sobrecarga (ej: Enemy_Pixel).")]
        [SerializeField] private GameObject minionPrefab;

        [Tooltip("Cantidad de esbirros generados por pulso en fase de furia.")]
        [SerializeField] private int minionsPerPulse = 2;

        [Tooltip("Multiplicador de velocidad al entrar en fase de sobrecarga final.")]
        [SerializeField] private float enragedSpeedMultiplier = 1.35f;

        [Tooltip("Efecto de transición visual al cambiar de fase.")]
        [SerializeField] private GameObject phaseChangeVfxPrefab;

        private int currentPhase = 1;
        private bool hasEnteredPhase2 = false;
        private bool hasEnteredPhase3 = false;

        public int CurrentPhase => currentPhase;

        protected override void Awake()
        {
            base.Awake();
            glitchName = "Mezcla Final";

            if (maxHealth <= 100f) maxHealth = 1500f;
            if (moveSpeed >= 1.0f) moveSpeed = 0.6f;
            if (coinsReward <= 15) coinsReward = 250;
            abilityInterval = 5f;
        }

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount);

            if (isDead) return;

            float hpPercentage = currentHealth / maxHealth;

            // Transición a Fase 2 (66% HP)
            if (!hasEnteredPhase2 && hpPercentage <= 0.66f)
            {
                hasEnteredPhase2 = true;
                TransitionToPhase(2);
            }
            // Transición a Fase 3 (33% HP)
            else if (!hasEnteredPhase3 && hpPercentage <= 0.33f)
            {
                hasEnteredPhase3 = true;
                TransitionToPhase(3);
            }
        }

        private void TransitionToPhase(int newPhase)
        {
            currentPhase = newPhase;

            if (phaseChangeVfxPrefab != null)
            {
                Instantiate(phaseChangeVfxPrefab, transform.position, Quaternion.identity);
            }

            if (currentPhase == 3)
            {
                // En fase 3 acelera en sobrecarga
                moveSpeed *= enragedSpeedMultiplier;
                abilityInterval = 4f; // Habilidades más frecuentes
            }

            Debug.Log($"[BossFinalMix] ¡Mezcla Final ha entrado en la FASE {currentPhase}!");
        }

        /// <summary>
        /// Ejecuta las habilidades según la fase activa del combate.
        /// </summary>
        protected override void ExecuteBossAbility()
        {
            base.ExecuteBossAbility();

            switch (currentPhase)
            {
                case 1:
                    DeployNoiseZone();
                    break;

                case 2:
                    EmitSilencePulse();
                    DeployNoiseZone();
                    break;

                case 3:
                    // Fase final: mezcla absoluta de todas las habilidades
                    EmitSilencePulse();
                    DeployNoiseZone();
                    SpawnOverloadMinions();
                    break;
            }
        }

        private void DeployNoiseZone()
        {
            if (noiseZonePrefab != null)
            {
                Vector3 spawnPos = transform.position;
                spawnPos.y += 0.02f;
                Instantiate(noiseZonePrefab, spawnPos, Quaternion.identity);
            }
        }

        private void EmitSilencePulse()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, silenceRadius);
            foreach (var col in colliders)
            {
                if (col.CompareTag("Tower"))
                {
                    Tower tower = col.GetComponent<Tower>();
                    if (tower != null && !tower.IsSilenced)
                    {
                        tower.Silence(silenceDuration);
                    }
                }
            }

            if (feedbackPulseVfxPrefab != null)
            {
                Instantiate(feedbackPulseVfxPrefab, transform.position, Quaternion.identity);
            }
        }

        private void SpawnOverloadMinions()
        {
            if (minionPrefab == null || waypoints == null) return;

            for (int i = 0; i < minionsPerPulse; i++)
            {
                Vector2 circle = Random.insideUnitCircle * 0.2f;
                Vector3 spawnPos = transform.position + new Vector3(circle.x, 0f, circle.y);

                GameObject minionObj = Instantiate(minionPrefab, spawnPos, transform.rotation, transform.parent);
                Enemy minion = minionObj.GetComponent<Enemy>();
                if (minion != null)
                {
                    minion.InitializePath(waypoints, currentWaypointIndex, spawnPos);
                }
            }
        }
    }
}
