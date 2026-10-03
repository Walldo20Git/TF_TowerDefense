using UnityEngine;
using ConcertDefense.Towers;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Zona de ruido y distorsión generada por el Jefe Distorsión:
    /// Ralentiza los proyectiles de las torres que atraviesan su área de efecto.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NoiseZone : MonoBehaviour
    {
        [Tooltip("Tiempo de permanencia en segundos antes de disiparse.")]
        [SerializeField] private float duration = 8f;

        [Tooltip("Factor de reducción de velocidad para los proyectiles que atraviesan la zona (0.5 = 50% de velocidad).")]
        [SerializeField] private float slowFactor = 0.4f;

        private void Start()
        {
            Destroy(gameObject, duration);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Si un proyectil entra en la zona de ruido, reducimos su velocidad
            Projectile projectile = other.GetComponent<Projectile>();
            if (projectile != null)
            {
                Rigidbody rb = projectile.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity *= slowFactor;
                }
            }
        }
    }

    /// <summary>
    /// Jefe de la Oleada 1: Distorsión (GDD 5):
    /// - Gran cantidad de vida y avance pausado.
    /// - Periódicamente deja tras de sí 'Zonas de Ruido' que entorpecen y ralentizan los proyectiles de las torres.
    /// </summary>
    public class BossDistortion : BossBase
    {
        [Header("Habilidad Especial: Zonas de Ruido")]
        [Tooltip("Prefab del área de distorsión/ruido estático que se instancia sobre el camino.")]
        [SerializeField] private GameObject noiseZonePrefab;

        [Tooltip("Efecto de pulso glitch al detonar la habilidad.")]
        [SerializeField] private GameObject glitchPulseVfxPrefab;

        protected override void Awake()
        {
            base.Awake();
            glitchName = "Distorsión";
            // Valores específicos del GDD: Mucha vida y lento
            if (maxHealth <= 100f) maxHealth = 600f;
            if (moveSpeed >= 1.0f) moveSpeed = 0.65f;
            if (coinsReward <= 15) coinsReward = 100;
        }

        /// <summary>
        /// Ejecuta la habilidad periódica: deposita una zona de ruido estático en su posición actual.
        /// </summary>
        protected override void ExecuteBossAbility()
        {
            base.ExecuteBossAbility();

            if (noiseZonePrefab != null)
            {
                Vector3 spawnPos = transform.position;
                spawnPos.y += 0.02f; // Ligeramente por encima de la superficie del camino

                Instantiate(noiseZonePrefab, spawnPos, Quaternion.identity);
            }

            if (glitchPulseVfxPrefab != null)
            {
                Instantiate(glitchPulseVfxPrefab, transform.position, Quaternion.identity);
            }

            Debug.Log("[BossDistortion] ¡Distorsión ha desplegado una zona de ruido!");
        }
    }
}
