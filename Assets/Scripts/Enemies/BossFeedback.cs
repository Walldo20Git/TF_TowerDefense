using UnityEngine;
using ConcertDefense.Towers;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe de la Oleada 2: Feedback (GDD 5):
    /// - Emite periódicamente un pulso de acople acústico / interferencia electromagnética.
    /// - Silencia las torres dentro de su radio de efecto durante varios segundos.
    /// - El jugador debe teletransportar o mover al avatar cerca de las torres afectadas para afinarlas y reactivarlas.
    /// </summary>
    public class BossFeedback : BossBase
    {
        [Header("Mecánica Feedback: Silenciar Torres")]
        [Tooltip("Radio del pulso expansivo que silencia las torres en metros.")]
        [SerializeField] private float silenceRadius = 2.0f;

        [Tooltip("Duración en segundos del silenciado en las torres impactadas.")]
        [SerializeField] private float silenceDuration = 6.0f;

        [Tooltip("Prefab del efecto visual de onda de interferencia expansiva.")]
        [SerializeField] private GameObject feedbackPulseVfxPrefab;

        [Tooltip("Sonido de acople / chirrido de audio al emitir el pulso.")]
        [SerializeField] private AudioClip feedbackSfx;

        private AudioSource audioSource;

        protected override void Awake()
        {
            base.Awake();
            glitchName = "Feedback";

            if (maxHealth <= 100f) maxHealth = 800f;
            if (moveSpeed >= 1.0f) moveSpeed = 0.75f;
            if (coinsReward <= 15) coinsReward = 140;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && feedbackSfx != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.8f;
            }
        }

        /// <summary>
        /// Emite la onda de acople acústico que silencia todas las torres en el radio.
        /// </summary>
        protected override void ExecuteBossAbility()
        {
            base.ExecuteBossAbility();

            // 1. Barrido de torres en el radio
            Collider[] colliders = Physics.OverlapSphere(transform.position, silenceRadius);
            int silencedCount = 0;

            foreach (var col in colliders)
            {
                if (col.CompareTag("Tower"))
                {
                    Tower tower = col.GetComponent<Tower>();
                    if (tower != null && !tower.IsSilenced)
                    {
                        tower.Silence(silenceDuration);
                        silencedCount++;
                    }
                }
            }

            // 2. Efecto visual de pulso expansivo
            if (feedbackPulseVfxPrefab != null)
            {
                Instantiate(feedbackPulseVfxPrefab, transform.position, Quaternion.identity);
            }

            // 3. Audio de acople
            if (audioSource != null && feedbackSfx != null)
            {
                audioSource.PlayOneShot(feedbackSfx);
            }

            Debug.Log($"[BossFeedback] ¡Feedback emitió un pulso de interferencia! {silencedCount} torres silenciadas.");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, silenceRadius);
        }
    }
}
