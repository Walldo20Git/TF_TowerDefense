using System;
using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.Towers;

namespace ConcertDefense.Enemies
{
    public enum BossType
    {
        Distortion,  // Oleada 1: zonas de ruido que ralentizan proyectiles
        Feedback,    // Oleada 2: silencia torres
        GlitchQueen, // Oleada 3: se divide en glitches más pequeños
        FinalMix     // Oleada 4: varias fases combinando habilidades
    }

    /// <summary>
    /// Clase base de los jefes de final de oleada (GDD 5):
    /// - Avisa a la UI al aparecer y al salir del campo (barra de vida superior).
    /// - Temporizador de habilidad y utilidades comunes (zonas de ruido, silenciar torres, esbirros).
    /// </summary>
    public class BossBase : Enemy
    {
        [Header("Configuración de Jefe (GDD 5)")]
        [SerializeField] private BossType bossType = BossType.Distortion;

        [Tooltip("Nombre del jefe para la barra superior.")]
        [SerializeField] private string bossTitle = "Distorsión";

        [Tooltip("Segundos entre usos de la habilidad especial.")]
        [SerializeField] protected float abilityInterval = 6f;

        [Tooltip("Efecto visual al usar la habilidad.")]
        [SerializeField] protected GameObject abilityVfxPrefab;

        protected float abilityTimer;

        protected override float DeathVfxSize => 2.5f;

        public BossType Type => bossType;
        public string BossTitle => bossTitle;

        public static event Action<BossBase> OnBossSpawned;
        /// <summary>El jefe salió del campo: derrotado o porque llegó al escenario.</summary>
        public static event Action<BossBase> OnBossDefeated;

        protected override void Start()
        {
            base.Start();

            abilityTimer = abilityInterval;
            OnBossSpawned?.Invoke(this);
            Sfx.Play(SfxId.BossArrive);
            GameMessages.Show($"¡Llega el jefe {bossTitle}!", 3f);
        }

        protected override void Update()
        {
            base.Update();
            if (isDead) return;

            abilityTimer -= Time.deltaTime;
            if (abilityTimer <= 0f)
            {
                abilityTimer = abilityInterval;
                ExecuteBossAbility();
            }
        }

        /// <summary>
        /// Cada jefe sobreescribe este método con su mecánica propia.
        /// </summary>
        protected virtual void ExecuteBossAbility()
        {
        }

        protected override void OnRemovedFromField(bool defeated)
        {
            OnBossDefeated?.Invoke(this);
        }

        // ---------- Utilidades compartidas por los jefes ----------

        /// <summary>Deja una zona de ruido en el suelo, bajo el jefe.</summary>
        protected void DeployNoiseZone(GameObject noiseZonePrefab)
        {
            if (noiseZonePrefab == null || Battlefield.Instance == null) return;

            Vector3 pos = transform.position;
            pos.y -= (hoverHeight - 0.012f) * Battlefield.Scale;
            Transform field = Battlefield.Instance.transform;
            Instantiate(noiseZonePrefab, pos, field.rotation, field);
        }

        /// <summary>Silencia las torres dentro del radio (unidades de campo). Devuelve cuántas.</summary>
        protected int SilenceTowers(float radius, float duration)
        {
            float worldRadius = radius * Battlefield.Scale;
            int count = 0;

            for (int i = Tower.All.Count - 1; i >= 0; i--)
            {
                Tower tower = Tower.All[i];
                if (tower.IsSilenced) continue;
                if (Vector3.Distance(tower.transform.position, transform.position) > worldRadius) continue;

                tower.Silence(duration);
                count++;
            }

            PulseEffect.Spawn(abilityVfxPrefab, transform.position, radius * 2f);
            Sfx.Play(SfxId.Feedback);
            return count;
        }

        /// <summary>Crea esbirros alrededor del jefe que continúan por el mismo camino.</summary>
        protected void SpawnMinions(GameObject minionPrefab, int count, float spread)
        {
            if (minionPrefab == null || waypoints == null || waypoints.Length == 0) return;

            int index = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Length - 1);

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * (spread * Battlefield.Scale);
                Vector3 spawnPos = transform.position + new Vector3(offset.x, 0f, offset.y);

                GameObject obj = Instantiate(minionPrefab, transform.parent);
                Enemy minion = obj.GetComponent<Enemy>();
                if (minion == null) continue;

                minion.InitializePath(waypoints, index, spawnPos);
                if (WaveSpawner.Instance != null) WaveSpawner.Instance.RegisterEnemy(minion);
            }
        }
    }
}
