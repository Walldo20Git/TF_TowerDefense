using UnityEngine;
using ConcertDefense.Enemies;

namespace ConcertDefense.Towers
{
    /// <summary>
    /// Gestiona la trayectoria, física de colisión, daño directo, daño en área y efectos de estado (ralentización y empuje)
    /// generados por los proyectiles de las torres (ondas de sonido, notas musicales, puerros, etc.).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        [Header("Comportamiento de Vuelo")]
        [Tooltip("Velocidad de avance del proyectil.")]
        [SerializeField] private float speed = 6f;

        [Tooltip("Si es true, persigue activamente al objetivo en movimiento.")]
        [SerializeField] private bool isHoming = true;

        [Tooltip("Velocidad de giro al perseguir al objetivo.")]
        [SerializeField] private float turnRate = 12f;

        [Tooltip("Tiempo de vida máximo en segundos antes de autodestruirse.")]
        [SerializeField] private float maxLifetime = 4f;

        [Header("Efectos de Impacto")]
        [Tooltip("Radio de daño en área (0 para daño a objetivo único).")]
        [SerializeField] private float splashRadius = 0f;

        [Tooltip("Fuerza de empuje físico transmitida al enemigo impactado (para torre Drop).")]
        [SerializeField] private float knockbackForce = 0f;

        [Tooltip("Factor de ralentización (1 = sin efecto, 0.5 = 50% de velocidad para torre Echo).")]
        [SerializeField] private float slowFactor = 1f;

        [Tooltip("Duración en segundos de la ralentización.")]
        [SerializeField] private float slowDuration = 0f;

        [Tooltip("Prefab de partículas instanciado al colisionar.")]
        [SerializeField] private GameObject impactVfxPrefab;

        // Variables dinámicas asignadas al disparar
        private Transform targetTransform;
        private Vector3 targetLastKnownPosition;
        private float damage = 25f;
        private Rigidbody rb;
        private bool hasHit = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            Destroy(gameObject, maxLifetime);
        }

        /// <summary>
        /// Inicializa los parámetros de combate del proyectil al ser disparado por una torre.
        /// </summary>
        public void Initialize(Transform target, float projectileDamage, float bonusSpeed = 0f, float customSplash = -1f, float customKnockback = -1f, float customSlow = -1f, float customSlowDuration = -1f)
        {
            targetTransform = target;
            damage = projectileDamage;

            if (targetTransform != null)
            {
                targetLastKnownPosition = targetTransform.position;
            }

            if (bonusSpeed > 0f) speed += bonusSpeed;
            if (customSplash >= 0f) splashRadius = customSplash;
            if (customKnockback >= 0f) knockbackForce = customKnockback;
            if (customSlow >= 0f) slowFactor = customSlow;
            if (customSlowDuration >= 0f) slowDuration = customSlowDuration;
        }

        private void Update()
        {
            if (hasHit) return;

            // Actualizar última posición conocida del objetivo si aún existe
            if (targetTransform != null)
            {
                targetLastKnownPosition = targetTransform.position;
            }

            Vector3 direction = (targetLastKnownPosition - transform.position).normalized;

            if (isHoming && direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnRate);
            }

            // Desplazamiento
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = transform.forward * speed;
            }
            else
            {
                transform.position += transform.forward * (speed * Time.deltaTime);
            }

            // Si el proyectil llegó a la última posición conocida y no había objetivo vivo, impacta
            if (Vector3.Distance(transform.position, targetLastKnownPosition) < 0.1f)
            {
                OnImpact(null);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            if (other.CompareTag("Enemy"))
            {
                Enemy enemy = other.GetComponent<Enemy>();
                OnImpact(enemy);
            }
        }

        /// <summary>
        /// Aplica daño directo o en área, empuje, ralentización y destruye el proyectil.
        /// </summary>
        private void OnImpact(Enemy directHitEnemy)
        {
            if (hasHit) return;
            hasHit = true;

            if (splashRadius > 0.05f)
            {
                // Daño en área (AoE)
                Collider[] colliders = Physics.OverlapSphere(transform.position, splashRadius);
                foreach (Collider col in colliders)
                {
                    if (col.CompareTag("Enemy"))
                    {
                        Enemy enemy = col.GetComponent<Enemy>();
                        if (enemy != null)
                        {
                            ApplyEffectsToEnemy(enemy);
                        }
                    }
                }
            }
            else if (directHitEnemy != null)
            {
                // Daño a objetivo único
                ApplyEffectsToEnemy(directHitEnemy);
            }

            // Instanciar efecto visual
            if (impactVfxPrefab != null)
            {
                Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Aplica daño y estados a un enemigo individual.
        /// </summary>
        private void ApplyEffectsToEnemy(Enemy enemy)
        {
            if (enemy == null || enemy.IsDead) return;

            // 1. Daño
            enemy.TakeDamage(damage);

            // 2. Ralentización
            if (slowFactor < 1f && slowDuration > 0f)
            {
                enemy.ApplySlow(slowFactor, slowDuration);
            }

            // 3. Empuje físico hacia atrás
            if (knockbackForce > 0f)
            {
                Vector3 pushDirection = (enemy.transform.position - transform.position).normalized;
                pushDirection.y = 0.1f; // Ligero impulso ascendente para evitar fricción en suelo
                enemy.ApplyKnockback(pushDirection * knockbackForce);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (splashRadius > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, splashRadius);
            }
        }
    }
}
