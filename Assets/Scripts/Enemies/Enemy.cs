using System;
using System.Collections;
using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Controla el comportamiento base de los enemigos (Glitches):
    /// - Movimiento a lo largo de waypoints en el escenario.
    /// - Sistema de salud, daño recibido y efectos de estado (ralentización, empuje).
    /// - Recompensa en monedas al morir y daño al Ánimo del escenario al llegar a la meta.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Enemy : MonoBehaviour
    {
        [Header("Atributos Base")]
        [Tooltip("Nombre identificador del Glitch (ej: Pixel, Static, Amp).")]
        [SerializeField] protected string glitchName = "Pixel";

        [Tooltip("Vida máxima del enemigo.")]
        [SerializeField] protected float maxHealth = 100f;

        [Tooltip("Velocidad de avance en metros por segundo.")]
        [SerializeField] protected float moveSpeed = 1.2f;

        [Tooltip("Monedas otorgadas al jugador cuando este glitch es destruido.")]
        [SerializeField] protected int coinsReward = 15;

        [Tooltip("Daño causado al Ánimo del escenario si este enemigo alcanza la meta.")]
        [SerializeField] protected int stageDamage = 1;

        [Header("Efectos")]
        [Tooltip("Prefab de partículas instanciado al ser derrotado.")]
        [SerializeField] protected GameObject deathVfxPrefab;

        // Variables de estado
        protected float currentHealth;
        protected Transform[] waypoints;
        protected int currentWaypointIndex = 0;
        protected float currentSpeedModifier = 1f;
        protected Coroutine slowCoroutine;
        protected Rigidbody rb;
        protected bool isDead = false;

        // Propiedades públicas
        public string GlitchName => glitchName;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;

        // Eventos
        public event Action<float, float> OnHealthChanged; // (actual, máxima)
        public event Action<Enemy> OnEnemyDeath;
        public event Action<Enemy> OnEnemyReachedEnd;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        protected virtual void Start()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Inicializa la ruta de waypoints asignada por el WaveSpawner o al dividirse de un jefe.
        /// </summary>
        public void InitializePath(Transform[] pathWaypoints, int startIndex = 0, Vector3? customSpawnPos = null)
        {
            waypoints = pathWaypoints;
            currentWaypointIndex = Mathf.Clamp(startIndex, 0, waypoints != null ? waypoints.Length : 0);

            if (customSpawnPos.HasValue)
            {
                transform.position = customSpawnPos.Value;
            }
            else if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                transform.position = waypoints[0].position;
            }
        }

        protected virtual void Update()
        {
            if (isDead) return;

            MoveAlongPath();
        }

        /// <summary>
        /// Mueve y orienta al enemigo hacia el waypoint objetivo actual.
        /// </summary>
        protected virtual void MoveAlongPath()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            if (currentWaypointIndex >= waypoints.Length) return;

            Transform targetWaypoint = waypoints[currentWaypointIndex];
            if (targetWaypoint == null) return;

            Vector3 targetPosition = targetWaypoint.position;
            // Mantener la misma altura para un movimiento plano y estable
            targetPosition.y = transform.position.y;

            Vector3 direction = (targetPosition - transform.position).normalized;
            float step = moveSpeed * currentSpeedModifier * Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            // Orientación suave hacia el avance
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
            }

            // Comprobar si se ha llegado al waypoint actual
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                currentWaypointIndex++;

                // Si se alcanzaron todos los waypoints, el enemigo ha llegado al escenario
                if (currentWaypointIndex >= waypoints.Length)
                {
                    ReachStage();
                }
            }
        }

        /// <summary>
        /// Aplica daño al enemigo. Reduce la vida, emite eventos y gestiona la muerte.
        /// </summary>
        public virtual void TakeDamage(float amount)
        {
            if (isDead) return;

            currentHealth -= amount;
            currentHealth = Mathf.Max(0f, currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Ralentiza la velocidad del enemigo durante un tiempo determinado (ej: torre Echo).
        /// </summary>
        public void ApplySlow(float slowFactor, float duration)
        {
            if (isDead) return;

            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
            }
            slowCoroutine = StartCoroutine(SlowRoutine(slowFactor, duration));
        }

        private IEnumerator SlowRoutine(float slowFactor, float duration)
        {
            currentSpeedModifier = Mathf.Clamp01(slowFactor);
            yield return new WaitForSeconds(duration);
            currentSpeedModifier = 1f;
            slowCoroutine = null;
        }

        /// <summary>
        /// Aplica un empuje físico al enemigo en una dirección dada (ej: torre Drop).
        /// </summary>
        public void ApplyKnockback(Vector3 force)
        {
            if (isDead) return;

            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(force, ForceMode.Impulse);
            }
            else
            {
                // Empuje directo sobre posición si no usa físicas activas
                transform.position += force * 0.1f;
            }
        }

        /// <summary>
        /// Se ejecuta cuando el enemigo alcanza la meta final del escenario.
        /// </summary>
        private void ReachStage()
        {
            if (isDead) return;
            isDead = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TakeStageDamage(stageDamage);
            }

            OnEnemyReachedEnd?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Se ejecuta cuando la vida llega a 0. Otorga monedas y reproduce efectos.
        /// </summary>
        private void Die()
        {
            if (isDead) return;
            isDead = true;

            // Recompensa en monedas
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(coinsReward);
            }

            // Efecto visual de muerte
            if (deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            OnEnemyDeath?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
