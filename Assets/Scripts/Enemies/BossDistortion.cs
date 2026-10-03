using UnityEngine;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe de la Oleada 1: Distorsión (GDD 5).
    /// Mucha vida y lento. Cada cierto tiempo deja una zona de ruido que ralentiza los proyectiles.
    /// </summary>
    public class BossDistortion : BossBase
    {
        [Header("Habilidad: Zonas de Ruido")]
        [Tooltip("Prefab de la zona de ruido (con componente NoiseZone).")]
        [SerializeField] private GameObject noiseZonePrefab;

        protected override void ExecuteBossAbility()
        {
            DeployNoiseZone(noiseZonePrefab);
        }
    }
}
