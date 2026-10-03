using UnityEngine;
using ConcertDefense.Towers;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Zona de ruido que deja el jefe Distorsión (GDD 5):
    /// los proyectiles que la atraviesan van más lentos mientras estén dentro.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NoiseZone : MonoBehaviour
    {
        [Tooltip("Segundos que permanece antes de disiparse.")]
        [SerializeField] private float duration = 8f;

        [Tooltip("Velocidad de los proyectiles dentro de la zona (0.35 = 35 %).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float projectileSpeedFactor = 0.35f;

        private void Start()
        {
            Destroy(gameObject, duration);
        }

        private void OnTriggerEnter(Collider other)
        {
            Projectile projectile = other.GetComponent<Projectile>();
            if (projectile != null) projectile.SpeedMultiplier = projectileSpeedFactor;
        }

        private void OnTriggerExit(Collider other)
        {
            Projectile projectile = other.GetComponent<Projectile>();
            if (projectile != null) projectile.SpeedMultiplier = 1f;
        }
    }
}
