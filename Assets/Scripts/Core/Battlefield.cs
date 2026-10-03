using UnityEngine;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Raíz del prefab del campo (GDD 4.1). Toda la lógica vive dentro de él y las distancias
    /// se expresan en "unidades de campo": al escalar o rotar el campo, velocidades y rangos siguen cuadrando.
    /// </summary>
    public class Battlefield : MonoBehaviour
    {
        public static Battlefield Instance { get; private set; }

        [Header("Contenedores (GDD 11)")]
        [SerializeField] private Transform stage;
        [SerializeField] private Transform path;
        [SerializeField] private Transform rollerCoasterTrack;
        [SerializeField] private GameObject rollerCoasterCart;
        [SerializeField] private Transform avatar;
        [SerializeField] private Transform towers;
        [SerializeField] private Transform enemies;
        [SerializeField] private Transform projectiles;

        public Transform Stage => stage;
        public Transform Avatar => avatar;
        public Transform Towers => towers != null ? towers : transform;
        public Transform Enemies => enemies != null ? enemies : transform;
        public Transform Projectiles => projectiles != null ? projectiles : transform;
        public GameObject RollerCoasterCart => rollerCoasterCart;

        /// <summary>Factor para convertir unidades de campo a metros del mundo.</summary>
        public static float Scale => Instance != null ? Instance.transform.lossyScale.x : 1f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Waypoints del camino de los enemigos, en orden.</summary>
        public Transform[] GetPathWaypoints()
        {
            return GetChildren(path);
        }

        /// <summary>Waypoints de la vía de la montaña rusa, en orden.</summary>
        public Transform[] GetTrackWaypoints()
        {
            return GetChildren(rollerCoasterTrack);
        }

        private static Transform[] GetChildren(Transform parent)
        {
            if (parent == null) return new Transform[0];

            var result = new Transform[parent.childCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = parent.GetChild(i);
            }
            return result;
        }
    }
}
