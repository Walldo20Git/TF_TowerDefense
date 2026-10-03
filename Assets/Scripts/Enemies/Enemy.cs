using System;
using System.Collections.Generic;
using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.UI;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Comportamiento base de los enemigos (Glitches, GDD 5):
    /// - Avanza por los waypoints del camino en unidades de campo.
    /// - Vida, ralentización (torre Echo) y empuje con Rigidbody (torre Drop / Roller Coaster).
    /// - Da monedas al morir y resta Ánimo al llegar al escenario.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Enemy : MonoBehaviour
    {
        /// <summary>Enemigos vivos en el campo. Lo usan torres y proyectiles para buscar objetivo.</summary>
        public static readonly List<Enemy> All = new List<Enemy>();

        [Header("Atributos Base")]
        [Tooltip("Nombre identificador del Glitch (ej: Pixel, Static, Amp).")]
        [SerializeField] protected string glitchName = "Pixel";

        [Tooltip("Vida máxima del enemigo.")]
        [SerializeField] protected float maxHealth = 40f;

        [Tooltip("Velocidad de avance en unidades de campo por segundo.")]
        [SerializeField] protected float moveSpeed = 0.22f;

        [Tooltip("Monedas otorgadas al jugador cuando este glitch es destruido.")]
        [SerializeField] protected int coinsReward = 8;

        [Tooltip("Ánimo que resta al escenario si alcanza la meta (1 glitch, 5 jefe).")]
        [SerializeField] protected int stageDamage = 1;

        [Tooltip("Altura sobre el camino a la que flota, en unidades de campo.")]
        [SerializeField] protected float hoverHeight = 0.06f;

        [Tooltip("Resistencia al empuje (0 = sale volando, 1 = inmune).")]
        [Range(0f, 1f)]
        [SerializeField] protected float knockbackResistance = 0f;

        [Header("Efectos y UI")]
        [Tooltip("Efecto instanciado al ser derrotado.")]
        [SerializeField] protected GameObject deathVfxPrefab;

        [Tooltip("Prefab de la barra de vida flotante (Canvas World Space).")]
        [SerializeField] protected GameObject healthBarPrefab;

        [Tooltip("Altura de la barra de vida sobre el enemigo, en unidades de campo.")]
        [SerializeField] protected float healthBarHeight = 0.14f;

        // Estado
        protected float currentHealth;
        protected Transform[] waypoints;
        protected int currentWaypointIndex;
        protected Rigidbody rb;
        protected bool isDead;

        private float slowFactor = 1f;
        private float slowTimer;
        private float knockbackTimer;

        public string GlitchName => glitchName;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;

        /// <summary>Cuánto ha avanzado por el camino; mayor = más cerca del escenario.</summary>
        public float PathProgress
        {
            get
            {
                if (waypoints == null || currentWaypointIndex >= waypoints.Length) return float.MaxValue;
                float remaining = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position) / Battlefield.Scale;
                return currentWaypointIndex * 10f - remaining;
            }
        }

        /// <summary>Tamaño relativo del efecto de muerte.</summary>
        protected virtual float DeathVfxSize => 1f;

        public event Action<float, float> OnHealthChanged; // (actual, máxima)
        public event Action<Enemy> OnEnemyDeath;
        public event Action<Enemy> OnEnemyReachedEnd;
        /// <summary>Se emite una sola vez cuando el enemigo sale del campo, por muerte o por llegar a la meta.</summary>
        public event Action<Enemy> OnRemoved;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            currentHealth = maxHealth;
        }

        protected virtual void OnEnable()
        {
            All.Add(this);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
        }

        protected virtual void Start()
        {
            if (healthBarPrefab != null)
            {
                GameObject bar = Instantiate(healthBarPrefab);
                WorldHealthBar healthBar = bar.GetComponent<WorldHealthBar>();
                if (healthBar != null) healthBar.Bind(this, healthBarHeight);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Asigna la ruta. <paramref name="startIndex"/> es el waypoint hacia el que avanza
        /// y <paramref name="customSpawnPos"/> permite nacer en medio del camino (esbirros de jefe).
        /// </summary>
        public void InitializePath(Transform[] pathWaypoints, int startIndex, Vector3? customSpawnPos)
        {
            waypoints = pathWaypoints;
            if (waypoints == null || waypoints.Length == 0) return;

            currentWaypointIndex = Mathf.Clamp(startIndex, 0, waypoints.Length - 1);
            Vector3 spawn = customSpawnPos ?? waypoints[currentWaypointIndex].position;
            spawn.y = waypoints[currentWaypointIndex].position.y + hoverHeight * Battlefield.Scale;
            transform.position = spawn;
        }

        protected virtual void Update()
        {
            if (isDead) return;

            if (slowTimer > 0f)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0f) slowFactor = 1f;
            }

            // Mientras dura el empuje manda la física; después retoma el camino
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.deltaTime;
                if (knockbackTimer <= 0f) EndKnockback();
                return;
            }

            MoveAlongPath();
        }

        private void MoveAlongPath()
        {
            if (waypoints == null || currentWaypointIndex >= waypoints.Length) return;

            float scale = Battlefield.Scale;
            Vector3 target = waypoints[currentWaypointIndex].position + Vector3.up * (hoverHeight * scale);
            Vector3 toTarget = target - transform.position;
            float step = moveSpeed * slowFactor * scale * Time.deltaTime;

            if (toTarget.sqrMagnitude <= step * step)
            {
                transform.position = target;
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length) ReachStage();
                return;
            }

            transform.position += toTarget.normalized * step;

            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.000001f)
            {
                Quaternion look = Quaternion.LookRotation(flat);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 10f);
            }
        }

        /// <summary>
        /// Aplica daño. Emite eventos y gestiona la muerte.
        /// </summary>
        public virtual void TakeDamage(float amount)
        {
            if (isDead || amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f) Die();
        }

        /// <summary>
        /// Ralentiza al enemigo durante un tiempo (torre Echo). factor 0.5 = mitad de velocidad.
        /// </summary>
        public void ApplySlow(float factor, float duration)
        {
            if (isDead) return;

            slowFactor = Mathf.Min(slowFactor, Mathf.Clamp(factor, 0.1f, 1f));
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        /// <summary>
        /// Empuje físico (torre Drop, Roller Coaster): el Rigidbody deja de ser cinemático
        /// un instante y recibe el impulso, en unidades de campo por segundo.
        /// </summary>
        public void ApplyKnockback(Vector3 velocity)
        {
            if (isDead || rb == null) return;

            velocity *= (1f - knockbackResistance) * Battlefield.Scale;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 0.0001f) return;

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(velocity, ForceMode.VelocityChange);
            knockbackTimer = 0.35f;
        }

        private void EndKnockback()
        {
            if (rb == null) return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        private void ReachStage()
        {
            if (isDead) return;
            isDead = true;

            if (GameManager.Instance != null) GameManager.Instance.TakeStageDamage(stageDamage);

            OnEnemyReachedEnd?.Invoke(this);
            RemoveFromField(false);
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (GameManager.Instance != null) GameManager.Instance.AddCoins(coinsReward);

            PulseEffect.Spawn(deathVfxPrefab, transform.position, DeathVfxSize);
            Sfx.Play(SfxId.Impact);

            OnEnemyDeath?.Invoke(this);
            RemoveFromField(true);
        }

        private void RemoveFromField(bool defeated)
        {
            OnRemovedFromField(defeated);
            OnRemoved?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Gancho para jefes: se llama justo antes de destruir al enemigo.
        /// </summary>
        protected virtual void OnRemovedFromField(bool defeated)
        {
        }
    }
}
