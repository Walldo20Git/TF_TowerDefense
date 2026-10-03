using System;
using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.Player;

namespace ConcertDefense.Towers
{
    /// <summary>
    /// Plataforma de construcción (GDD 4.2 y 4.3):
    /// - Sabe si está libre u ocupada y crea la torre elegida descontando su costo.
    /// - Exige tener al avatar cerca para construir o mejorar (configurable con requireAvatarNearby).
    /// - Su indicador hace de retícula de torre: verde si se puede construir, rojo si el avatar está lejos.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuildSpot : MonoBehaviour
    {
        [Header("Reglas de Proximidad (GDD 4.2)")]
        [Tooltip("Si es true, exige tener al avatar cerca para construir o mejorar. Desactívalo si resulta incómodo.")]
        [SerializeField] private bool requireAvatarNearby = true;

        [Tooltip("Distancia máxima entre el avatar y la plataforma, en unidades de campo.")]
        [SerializeField] private float avatarProximityDistance = 0.45f;

        [Header("Visualización")]
        [Tooltip("Indicador de plataforma disponible (se oculta al construir).")]
        [SerializeField] private Renderer availableIndicator;

        [SerializeField] private Color inRangeColor = new Color(0.1f, 1f, 0.45f, 0.85f);
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.2f, 0.25f, 0.85f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Tower currentTower;
        private MaterialPropertyBlock block;
        private bool lastNearby;
        private bool colorInitialized;

        public bool RequireAvatarNearby
        {
            get => requireAvatarNearby;
            set => requireAvatarNearby = value;
        }

        public bool IsOccupied => currentTower != null;
        public Tower CurrentTower => currentTower;

        /// <summary>Se tocó una plataforma libre con el avatar cerca: abrir el selector de torres.</summary>
        public static event Action<BuildSpot> OnBuildSpotClicked;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (availableIndicator == null) return;

            bool free = !IsOccupied;
            if (availableIndicator.gameObject.activeSelf != free) availableIndicator.gameObject.SetActive(free);
            if (!free) return;

            bool nearby = IsAvatarNearby();
            if (colorInitialized && nearby == lastNearby) return;

            colorInitialized = true;
            lastNearby = nearby;
            block.SetColor(BaseColorId, nearby ? inRangeColor : outOfRangeColor);
            availableIndicator.SetPropertyBlock(block);
        }

        /// <summary>
        /// ¿Está el avatar lo bastante cerca de la plataforma?
        /// </summary>
        public bool IsAvatarNearby()
        {
            if (!requireAvatarNearby) return true;

            AvatarController avatar = AvatarController.Instance;
            if (avatar == null) return false;

            Vector3 delta = avatar.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude <= avatarProximityDistance * Battlefield.Scale;
        }

        /// <summary>
        /// Toque del jugador sobre la plataforma (lo enruta TouchInputRouter).
        /// </summary>
        public void HandleTap()
        {
            if (IsOccupied)
            {
                currentTower.Select();
                return;
            }

            if (IsAvatarNearby())
            {
                OnBuildSpotClicked?.Invoke(this);
                return;
            }

            // El avatar está lejos: va hacia la plataforma por su cuenta
            if (AvatarController.Instance != null)
            {
                AvatarController.Instance.SetDestinationNear(transform.position, avatarProximityDistance * 0.6f);
            }
            GameMessages.Show("La heroína va hacia la plataforma. Tócala de nuevo cuando esté en verde.");
        }

        /// <summary>
        /// Construye una torre en la plataforma si hay monedas y el avatar está cerca.
        /// </summary>
        public bool BuildTower(GameObject towerPrefab)
        {
            if (IsOccupied || towerPrefab == null || Battlefield.Instance == null) return false;

            Tower prefabTower = towerPrefab.GetComponent<Tower>();
            if (prefabTower == null)
            {
                Debug.LogError("[BuildSpot] El prefab asignado no tiene el componente Tower.");
                return false;
            }

            if (!IsAvatarNearby())
            {
                GameMessages.Show("Acerca a la heroína a la plataforma para construir.");
                return false;
            }

            if (GameManager.Instance == null || !GameManager.Instance.TrySpendCoins(prefabTower.BaseCost))
            {
                GameMessages.Show($"Monedas insuficientes: {prefabTower.TowerName} cuesta {prefabTower.BaseCost}.");
                return false;
            }

            // La torre cuelga del contenedor Towers, no de la plataforma, para no heredar su forma aplastada
            Transform field = Battlefield.Instance.transform;
            GameObject obj = Instantiate(towerPrefab, transform.position, field.rotation, Battlefield.Instance.Towers);
            currentTower = obj.GetComponent<Tower>();
            currentTower.Spot = this;

            Sfx.Play(SfxId.Build);
            GameMessages.Show($"Torre {currentTower.TowerName} construida.", 1.5f);
            return true;
        }

        /// <summary>
        /// Libera la plataforma (al vender la torre).
        /// </summary>
        public void ClearSpot()
        {
            currentTower = null;
            colorInitialized = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!requireAvatarNearby) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, avatarProximityDistance * transform.lossyScale.x);
        }
    }
}
