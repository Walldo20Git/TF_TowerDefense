using System;
using UnityEngine;
using UnityEngine.EventSystems;
using ConcertDefense.Core;

namespace ConcertDefense.Towers
{
    /// <summary>
    /// Plataforma sobre la cual el jugador puede construir torres:
    /// - Verifica la disponibilidad de la plataforma (libre / ocupada).
    /// - Controla la regla del GDD que exige tener al avatar cerca (parámetro configurable requireAvatarNearby).
    /// - Instancia la torre seleccionada deduciendo su costo de monedas y libera el espacio si la torre es vendida.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuildSpot : MonoBehaviour
    {
        [Header("Reglas de Proximidad (GDD 4.2)")]
        [Tooltip("Si es true, exige tener al avatar cerca de la plataforma para poder construir o mejorar.")]
        [SerializeField] private bool requireAvatarNearby = true;

        [Tooltip("Distancia máxima en metros permitida entre el avatar y la plataforma para habilitar la construcción.")]
        [SerializeField] private float avatarProximityDistance = 1.2f;

        [Header("Puntos de Anclaje y Visualización")]
        [Tooltip("Punto exacto donde se colocará la base de la torre (si es nulo, usa el centro de la plataforma).")]
        [SerializeField] private Transform towerMountPoint;

        [Tooltip("Indicador visual que resalta si la plataforma está disponible para construir.")]
        [SerializeField] private GameObject availableIndicator;

        [Tooltip("Color o material cuando el avatar está en rango (válido / verde anime).")]
        [SerializeField] private Color inRangeColor = new Color(0f, 1f, 0.8f, 0.6f);

        [Tooltip("Color cuando el avatar está fuera de rango (inválido / rojo).")]
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.2f, 0.3f, 0.6f);

        // Estado
        private Tower currentTower;
        private Transform cachedAvatar;

        public bool RequireAvatarNearby
        {
            get => requireAvatarNearby;
            set => requireAvatarNearby = value;
        }

        public bool IsOccupied => currentTower != null;
        public Tower CurrentTower => currentTower;

        // Evento notificado al tocar la plataforma libre para abrir selector de torres
        public static event Action<BuildSpot> OnBuildSpotClicked;

        private void Start()
        {
            if (towerMountPoint == null)
            {
                towerMountPoint = transform;
            }

            FindAvatar();
        }

        private void Update()
        {
            UpdateVisualIndicators();
        }

        /// <summary>
        /// Localiza al avatar en la escena mediante la etiqueta 'Avatar'.
        /// </summary>
        private void FindAvatar()
        {
            if (cachedAvatar == null)
            {
                GameObject avatarObj = GameObject.FindWithTag("Avatar");
                if (avatarObj != null)
                {
                    cachedAvatar = avatarObj.transform;
                }
            }
        }

        /// <summary>
        /// Verifica si el avatar se encuentra dentro del rango de proximidad requerido.
        /// </summary>
        public bool IsAvatarNearby()
        {
            if (!requireAvatarNearby) return true;

            if (cachedAvatar == null)
            {
                FindAvatar();
                if (cachedAvatar == null) return false;
            }

            float distance = Vector3.Distance(transform.position, cachedAvatar.position);
            return distance <= avatarProximityDistance;
        }

        /// <summary>
        /// Actualiza la visualización del indicador según si la plataforma está libre y el avatar cerca.
        /// </summary>
        private void UpdateVisualIndicators()
        {
            if (availableIndicator == null) return;

            // Si ya hay una torre construida, el indicador de plataforma libre permanece oculto
            if (IsOccupied)
            {
                availableIndicator.SetActive(false);
                return;
            }

            availableIndicator.SetActive(true);

            // Cambiar color según proximidad del avatar
            Renderer rend = availableIndicator.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = IsAvatarNearby() ? inRangeColor : outOfRangeColor;
            }
        }

        /// <summary>
        /// Instancia una nueva torre en esta plataforma si se cumplen los fondos y la regla de proximidad.
        /// </summary>
        public bool BuildTower(GameObject towerPrefab)
        {
            if (IsOccupied)
            {
                Debug.LogWarning("[BuildSpot] Esta plataforma ya tiene una torre instalada.");
                return false;
            }

            if (!IsAvatarNearby())
            {
                Debug.LogWarning("[BuildSpot] El avatar está demasiado lejos para construir en esta plataforma.");
                return false;
            }

            if (towerPrefab == null)
            {
                Debug.LogError("[BuildSpot] No se proporcionó prefab de torre válido.");
                return false;
            }

            Tower towerComponent = towerPrefab.GetComponent<Tower>();
            if (towerComponent == null)
            {
                Debug.LogError("[BuildSpot] El prefab asignado no tiene el componente Tower.");
                return false;
            }

            // Comprobar y descontar monedas
            int cost = towerComponent.TotalInvestedCoins;
            if (GameManager.Instance != null && !GameManager.Instance.TrySpendCoins(cost))
            {
                Debug.LogWarning($"[BuildSpot] Fondos insuficientes para construir {towerComponent.TowerName}.");
                return false;
            }

            // Instanciar torre en la plataforma
            Vector3 spawnPos = towerMountPoint != null ? towerMountPoint.position : transform.position;
            Quaternion spawnRot = towerMountPoint != null ? towerMountPoint.rotation : Quaternion.identity;

            GameObject newTowerObj = Instantiate(towerPrefab, spawnPos, spawnRot, transform);
            currentTower = newTowerObj.GetComponent<Tower>();

            Debug.Log($"[BuildSpot] ¡Torre {currentTower.TowerName} construida con éxito!");
            return true;
        }

        /// <summary>
        /// Libera la plataforma para que se pueda construir una nueva torre (llamado al vender).
        /// </summary>
        public void ClearSpot()
        {
            currentTower = null;
        }

        private void OnMouseDown()
        {
            // Ignorar clic si el cursor está sobre un elemento de UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Si está libre, dispara evento para abrir el menú de selección de torre
            if (!IsOccupied)
            {
                if (IsAvatarNearby())
                {
                    OnBuildSpotClicked?.Invoke(this);
                }
                else
                {
                    Debug.Log("[BuildSpot] Acércate con la heroína o desactiva 'requireAvatarNearby' para construir aquí.");
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (requireAvatarNearby)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, avatarProximityDistance);
            }
        }
    }
}
