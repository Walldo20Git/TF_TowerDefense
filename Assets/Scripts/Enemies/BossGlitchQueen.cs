using UnityEngine;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe de la Oleada 3: Reina Glitch (GDD 5):
    /// - Posee una gran masa de datos corrompidos (1000 HP).
    /// - Al recibir daño por tramos de salud (75%, 50%, 25%) y al morir, se divide
    ///   liberando múltiples glitches pequeños (Pixel) que continúan por la ruta hacia el escenario.
    /// </summary>
    public class BossGlitchQueen : BossBase
    {
        [Header("Mecánica División en Enjambre")]
        [Tooltip("Prefab del glitch menor generado al dividirse (ej: Enemy_Pixel).")]
        [SerializeField] private GameObject minionPrefab;

        [Tooltip("Cantidad de esbirros que emergen en cada umbral de vida (75%, 50%, 25%).")]
        [SerializeField] private int minionsPerThreshold = 2;

        [Tooltip("Cantidad de esbirros que se liberan en la explosión final al morir.")]
        [SerializeField] private int minionsOnDeath = 4;

        [Tooltip("Efecto de partículas de dispersión de píxeles al dividirse.")]
        [SerializeField] private GameObject splitVfxPrefab;

        // Control de umbrales para no repetir división en el mismo tramo
        private bool splitTriggered75 = false;
        private bool splitTriggered50 = false;
        private bool splitTriggered25 = false;

        protected override void Awake()
        {
            base.Awake();
            glitchName = "Reina Glitch";

            if (maxHealth <= 100f) maxHealth = 1000f;
            if (moveSpeed >= 1.0f) moveSpeed = 0.7f;
            if (coinsReward <= 15) coinsReward = 180;
        }

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount);

            if (isDead) return;

            float healthPercentage = currentHealth / maxHealth;

            // División al 75% de vida
            if (!splitTriggered75 && healthPercentage <= 0.75f)
            {
                splitTriggered75 = true;
                SpawnMinionSwarm(minionsPerThreshold);
            }
            // División al 50% de vida
            else if (!splitTriggered50 && healthPercentage <= 0.50f)
            {
                splitTriggered50 = true;
                SpawnMinionSwarm(minionsPerThreshold);
            }
            // División al 25% de vida
            else if (!splitTriggered25 && healthPercentage <= 0.25f)
            {
                splitTriggered25 = true;
                SpawnMinionSwarm(minionsPerThreshold);
            }
        }

        /// <summary>
        /// Genera un grupo de mini-glitches en la posición de la Reina y los incorpora a la ruta activa.
        /// </summary>
        private void SpawnMinionSwarm(int count)
        {
            if (minionPrefab == null || waypoints == null || waypoints.Length == 0) return;

            // Efecto visual de partición de píxeles
            if (splitVfxPrefab != null)
            {
                Instantiate(splitVfxPrefab, transform.position, Quaternion.identity);
            }

            for (int i = 0; i < count; i++)
            {
                // Dispersión aleatoria alrededor de la reina
                Vector2 randomCircle = Random.insideUnitCircle * 0.15f;
                Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

                GameObject minionObj = Instantiate(minionPrefab, spawnPos, transform.rotation, transform.parent);
                Enemy minion = minionObj.GetComponent<Enemy>();

                if (minion != null)
                {
                    // Asignar el camino a partir del waypoint en el que se encuentra la reina
                    minion.InitializePath(waypoints, currentWaypointIndex, spawnPos);
                }
            }

            Debug.Log($"[BossGlitchQueen] ¡La Reina Glitch se ha dividido, liberando {count} esbirros!");
        }

        /// <summary>
        /// Al ser derrotada, detona una última división en enjambre.
        /// </summary>
        protected override void ExecuteBossAbility()
        {
            base.ExecuteBossAbility();
            // Pulso pasivo de la reina (opcional: pequeño pulso de velocidad a esbirros cercanos)
        }

        private void OnDestroy()
        {
            // Si muere en combate, genera la explosión final de esbirros
            if (isDead)
            {
                SpawnMinionSwarm(minionsOnDeath);
            }
        }
    }
}
