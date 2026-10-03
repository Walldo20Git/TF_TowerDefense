using System;
using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Enemies
{
    public enum BossType
    {
        Distortion,  // Oleada 1: Ralentiza proyectiles en áreas de ruido
        Feedback,    // Oleada 2: Silencia torres
        GlitchQueen, // Oleada 3: Se divide en glitches más pequeños
        FinalMix     // Oleada 4: Múltiples fases combinando habilidades
    }

    /// <summary>
    /// Clase base para todos los Jefes de final de oleada (GDD 5):
    /// - Hereda de Enemy con estadísticas superiores y resta 5 puntos de Ánimo si alcanza el escenario.
    /// - Emite eventos estáticos de aparición y derrota para vincular la barra de vida superior de la pantalla.
    /// - Estructura base para las habilidades únicas de cada jefe.
    /// </summary>
    public class BossBase : Enemy
    {
        [Header("Configuración de Jefe (GDD 5)")]
        [Tooltip("Tipo de jefe asignado a esta oleada.")]
        [SerializeField] private BossType bossType = BossType.Distortion;

        [Tooltip("Título o subtítulo del jefe para la UI superior (ej: 'Distorsión - Corruptor de Sonido').")]
        [SerializeField] private string bossTitle = "Distorsión";

        [Tooltip("Intervalo en segundos entre usos de la habilidad especial del jefe.")]
        [SerializeField] protected float abilityInterval = 6f;

        [Header("Barra de Vida Flotante (World Space)")]
        [Tooltip("Prefab opcional de barra de vida flotante específico para el jefe.")]
        [SerializeField] private GameObject worldSpaceHealthBarPrefab;

        protected float abilityTimer = 0f;

        public BossType Type => bossType;
        public string BossTitle => bossTitle;

        // Eventos estáticos globales para la UI de pantalla y el WaveSpawner
        public static event Action<BossBase> OnBossSpawned;
        public static event Action<BossBase> OnBossDefeated;

        protected override void Awake()
        {
            base.Awake();
            // Según GDD 4.6: "cada jefe que llega resta 5 puntos de Ánimo"
            stageDamage = 5;
        }

        protected override void Start()
        {
            base.Start();

            // Instanciar barra de vida flotante sobre el jefe si está asignada
            if (worldSpaceHealthBarPrefab != null)
            {
                GameObject bar = Instantiate(worldSpaceHealthBarPrefab, transform.position, Quaternion.identity);
                var follow = bar.GetComponent<ConcertDefense.UI.UIFollow>();
                if (follow != null)
                {
                    follow.SetTarget(transform, new Vector3(0f, 0.6f, 0f));
                }
            }

            abilityTimer = abilityInterval;

            // Notificar a la UI superior que el jefe ha entrado al campo
            OnBossSpawned?.Invoke(this);
            Debug.Log($"[BossBase] ¡El Jefe {bossTitle} ha aparecido en el escenario!");
        }

        protected virtual void Update()
        {
            if (isDead) return;

            // Avance por los waypoints (heredado de Enemy)
            base.Update();

            // Temporizador para activar la habilidad especial del jefe
            abilityTimer -= Time.deltaTime;
            if (abilityTimer <= 0f)
            {
                ExecuteBossAbility();
                abilityTimer = abilityInterval;
            }
        }

        /// <summary>
        /// Método virtual que cada script de jefe específico sobreescribe para su mecánica propia.
        /// </summary>
        protected virtual void ExecuteBossAbility()
        {
            // Implementado por las clases hijas (Distortion, Feedback, etc.)
        }

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount);

            // Si fue destruido
            if (isDead)
            {
                OnBossDefeated?.Invoke(this);
            }
        }
    }
}
