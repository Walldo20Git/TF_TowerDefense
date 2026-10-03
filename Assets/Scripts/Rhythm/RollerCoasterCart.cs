using System.Collections.Generic;
using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.Rhythm
{
    /// <summary>
    /// Carrito de la montaña rusa (GDD 4.5). Su trigger daña y empuja a cada enemigo que toca,
    /// una sola vez por recorrido.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RollerCoasterCart : MonoBehaviour
    {
        [Tooltip("Radio de barrido alrededor del carrito, en unidades de campo (respaldo del trigger).")]
        [SerializeField] private float sweepRadius = 0.16f;

        private float damage = 250f;
        private float knockback = 1.8f;
        private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        public void Initialize(float cartDamage, float cartKnockback)
        {
            damage = cartDamage;
            knockback = cartKnockback;
            hitEnemies.Clear();
        }

        private void Update()
        {
            float worldRadius = sweepRadius * Battlefield.Scale;

            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                if (i >= Enemy.All.Count) continue;
                Enemy enemy = Enemy.All[i];

                Vector3 delta = enemy.transform.position - transform.position;
                delta.y = 0f;
                if (delta.magnitude <= worldRadius) Hit(enemy);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Enemy enemy = other.GetComponentInParent<Enemy>();
            if (enemy != null) Hit(enemy);
        }

        private void Hit(Enemy enemy)
        {
            if (enemy.IsDead || !hitEnemies.Add(enemy)) return;

            Vector3 push = transform.forward;
            push.y = 0f;
            enemy.ApplyKnockback(push.normalized * knockback);
            enemy.TakeDamage(damage);
        }
    }
}
