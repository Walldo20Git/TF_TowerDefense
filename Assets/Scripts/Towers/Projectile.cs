using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.Towers
{
    /// <summary>
    /// Proyectil de las torres (ondas, notas, puerros...). Tiene Rigidbody y collider trigger:
    /// persigue a su objetivo y al chocar aplica daño directo o en área, ralentización y empuje físico.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [Header("Vuelo")]
        [Tooltip("Velocidad en unidades de campo por segundo.")]
        [SerializeField] private float speed = 2.4f;

        [Tooltip("Segundos de vida antes de autodestruirse.")]
        [SerializeField] private float maxLifetime = 4f;

        [Header("Efectos de Impacto")]
        [Tooltip("Radio de daño en área, en unidades de campo (0 = un solo objetivo).")]
        [SerializeField] private float splashRadius = 0f;

        [Tooltip("Velocidad de empuje transmitida a los enemigos (torre Drop).")]
        [SerializeField] private float knockbackForce = 0f;

        [Tooltip("Factor de ralentización (1 = sin efecto, 0.5 = mitad de velocidad; torre Echo).")]
        [SerializeField] private float slowFactor = 1f;

        [Tooltip("Duración de la ralentización en segundos.")]
        [SerializeField] private float slowDuration = 0f;

        [Tooltip("Efecto visual al impactar.")]
        [SerializeField] private GameObject impactVfxPrefab;

        /// <summary>Lo reducen las zonas de ruido del jefe Distorsión.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        private Enemy target;
        private Vector3 lastTargetPosition;
        private float damage;
        private Rigidbody rb;
        private bool hasHit;
        private float lifetime;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
        }

        /// <summary>
        /// Lanza el proyectil contra un enemigo con el daño ya calculado por la torre.
        /// </summary>
        public void Launch(Enemy targetEnemy, float projectileDamage)
        {
            target = targetEnemy;
            damage = projectileDamage;
            if (target != null) lastTargetPosition = target.transform.position;
        }

        private void Update()
        {
            if (hasHit) return;

            lifetime += Time.deltaTime;
            if (lifetime >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            bool targetAlive = target != null && !target.IsDead;
            if (targetAlive) lastTargetPosition = target.transform.position;

            float scale = Battlefield.Scale;
            Vector3 toTarget = lastTargetPosition - transform.position;
            float stepDistance = speed * SpeedMultiplier * scale * Time.deltaTime;

            // Respaldo de la colisión física: si ya está encima del objetivo, impacta
            if (toTarget.magnitude <= Mathf.Max(stepDistance, 0.04f * scale))
            {
                Impact(targetAlive ? target : null);
                return;
            }

            Vector3 direction = toTarget.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            rb.linearVelocity = direction * (speed * SpeedMultiplier * scale);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            Enemy enemy = other.GetComponentInParent<Enemy>();
            if (enemy != null && !enemy.IsDead) Impact(enemy);
        }

        private void Impact(Enemy directHit)
        {
            if (hasHit) return;
            hasHit = true;

            if (splashRadius > 0.01f)
            {
                float worldRadius = splashRadius * Battlefield.Scale;

                // Recorrido inverso: un jefe puede crear esbirros al recibir daño
                for (int i = Enemy.All.Count - 1; i >= 0; i--)
                {
                    if (i >= Enemy.All.Count) continue;
                    Enemy enemy = Enemy.All[i];
                    if (Vector3.Distance(enemy.transform.position, transform.position) <= worldRadius)
                    {
                        ApplyEffects(enemy);
                    }
                }
            }
            else if (directHit != null)
            {
                ApplyEffects(directHit);
            }

            PulseEffect.Spawn(impactVfxPrefab, transform.position, splashRadius > 0.01f ? splashRadius * 8f : 1f);
            Sfx.Play(SfxId.Impact);
            Destroy(gameObject);
        }

        private void ApplyEffects(Enemy enemy)
        {
            if (enemy == null || enemy.IsDead) return;

            if (slowFactor < 1f && slowDuration > 0f) enemy.ApplySlow(slowFactor, slowDuration);

            if (knockbackForce > 0f)
            {
                Vector3 push = enemy.transform.position - transform.position;
                push.y = 0f;
                if (push.sqrMagnitude < 0.000001f) push = transform.forward;
                enemy.ApplyKnockback(push.normalized * knockbackForce);
            }

            enemy.TakeDamage(damage);
        }

        private void OnDrawGizmosSelected()
        {
            if (splashRadius > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, splashRadius * transform.lossyScale.x);
            }
        }
    }
}
