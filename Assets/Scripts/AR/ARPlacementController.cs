using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using ConcertDefense.Core;
using ConcertDefense.Rhythm;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Controla el escaneo de superficies AR, la proyección de la retícula en el centro
    /// de la pantalla y la colocación del prefab principal del campo de batalla (Battlefield).
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class ARPlacementController : MonoBehaviour
    {
        [Header("Referencias AR")]
        [Tooltip("Gestor de raycasts de AR Foundation (asignado automáticamente si está en el mismo GameObject).")]
        [SerializeField] private ARRaycastManager raycastManager;

        [Tooltip("Gestor de planos AR (opcional, para ocultar los planos tras colocar el campo).")]
        [SerializeField] private ARPlaneManager planeManager;

        [Header("Elementos de Colocación")]
        [Tooltip("Objeto visual de la retícula que sigue los planos detectados.")]
        [SerializeField] private GameObject placementReticle;

        [Tooltip("Prefab del campo de batalla que contiene todo el escenario, waypoints y plataformas.")]
        [SerializeField] private GameObject battlefieldPrefab;

        [Header("Ajustes")]
        [Tooltip("Si es true, desactiva la visualización de planos una vez colocado el escenario para mayor inmersión.")]
        [SerializeField] private bool hidePlanesAfterPlacement = true;

        // Instancia instanciada del campo de batalla
        private GameObject spawnedBattlefield;

        // Lista caché para almacenar los impactos de ARRaycast
        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        // Estado del raycast actual
        private bool placementPoseIsValid = false;
        private Pose placementPose;

        public GameObject SpawnedBattlefield => spawnedBattlefield;
        public bool IsPlaced => spawnedBattlefield != null;

        private void Awake()
        {
            if (raycastManager == null)
            {
                raycastManager = GetComponent<ARRaycastManager>();
            }

            if (planeManager == null)
            {
                planeManager = GetComponent<ARPlaneManager>();
            }

            // Destruir el Object Spawner predeterminado del template AR para evitar cubos azules al hacer clic
            GameObject spawner = GameObject.Find("Object Spawner");
            if (spawner != null)
            {
                spawner.SetActive(false);
                Destroy(spawner);
            }
        }

        private void Start()
        {
            if (placementReticle != null)
            {
                placementReticle.SetActive(false);
            }

            // Si el escenario ya está en la escena (preexistente o guardado en la escena), auto-vincularlo y pasar a Playing
            if (spawnedBattlefield == null)
            {
                GameObject existing = GameObject.Find("Battlefield(Clone)");
                if (existing == null) existing = GameObject.Find("Battlefield");
                if (existing != null)
                {
                    spawnedBattlefield = existing;
                    BindBattlefieldComponents(spawnedBattlefield);
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ChangeState(GameState.Playing);
                    }
                    Debug.Log("<color=#00FF88><b>[ARPlacementController] Battlefield preexistente detectado. Juego iniciado en estado Playing con HUD activo.</b></color>");
                }
            }
        }

        private void Update()
        {
            // Si el escenario ya está colocado, garantizar estado Playing, ocultar retícula y no procesar escaneo
            if (IsPlaced)
            {
                if (placementReticle != null && placementReticle.activeSelf)
                {
                    placementReticle.SetActive(false);
                }
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    GameManager.Instance.ChangeState(GameState.Playing);
                }
                return;
            }

            UpdatePlacementPose();
            UpdatePlacementReticle();

            // Detectar toque para fijar el escenario si la posición de la retícula es válida
            if (placementPoseIsValid && TryGetTouchInput(out Vector2 screenPosition))
            {
                if (!IsPointerOverUI(screenPosition))
                {
                    PlaceBattlefield();
                }
            }

            #if UNITY_EDITOR
            if (!IsPlaced && Input.GetKeyDown(KeyCode.Space))
            {
                placementPose = new Pose(new Vector3(0f, 0f, 1.2f), Quaternion.identity);
                PlaceBattlefield();
            }
            #endif
        }

        /// <summary>
        /// Lanza un raycast desde el centro de la pantalla hacia los planos detectados por ARCore.
        /// En el Editor de Unity, proyecta un raycast hacia el plano Y=0 para poder probar con un solo clic.
        /// </summary>
        private void UpdatePlacementPose()
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            bool hitAR = raycastManager != null && raycastManager.Raycast(screenCenter, s_Hits, TrackableType.PlaneWithinPolygon);

            if (hitAR)
            {
                placementPoseIsValid = true;
                placementPose = s_Hits[0].pose;

                // Orientar la retícula hacia adelante de la cámara pero plana sobre el plano horizontal
                Vector3 cameraForward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                Vector3 cameraBearing = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;

                if (cameraBearing != Vector3.zero)
                {
                    placementPose.rotation = Quaternion.LookRotation(cameraBearing, placementPose.up);
                }

                // Notificar al GameManager que pasamos a estado de colocación lista
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Scanning)
                {
                    GameManager.Instance.ChangeState(GameState.Placing);
                }
            }
            #if UNITY_EDITOR
            else if (Application.isEditor)
            {
                // Soporte para pruebas directas en el Editor de Unity sin requerir móvil AR
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 mousePos = Input.mousePosition;
                    if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
                    {
                        Ray ray = cam.ScreenPointToRay(mousePos);
                        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

                        if (groundPlane.Raycast(ray, out float enter))
                        {
                            placementPoseIsValid = true;
                            placementPose = new Pose(ray.GetPoint(enter), Quaternion.identity);

                            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Scanning)
                            {
                                GameManager.Instance.ChangeState(GameState.Placing);
                            }
                        }
                    }
                }
            }
            #endif
            else
            {
                placementPoseIsValid = false;

                // Si perdimos el plano, volvemos a estado de escaneo
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Placing)
                {
                    GameManager.Instance.ChangeState(GameState.Scanning);
                }
            }
        }

        /// <summary>
        /// Muestra u oculta la retícula y actualiza su posición según el raycast.
        /// </summary>
        private void UpdatePlacementReticle()
        {
            if (placementReticle == null) return;

            if (placementPoseIsValid)
            {
                placementReticle.SetActive(true);
                placementReticle.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
            }
            else
            {
                placementReticle.SetActive(false);
            }
        }

        /// <summary>
        /// Instancia el prefab del campo de batalla en la posición del plano y pasa al estado Playing.
        /// </summary>
        public void PlaceBattlefield()
        {
            if (battlefieldPrefab == null)
            {
                Debug.LogError("[ARPlacementController] No se ha asignado el prefab del campo de batalla (battlefieldPrefab).");
                return;
            }

            if (spawnedBattlefield == null)
            {
                spawnedBattlefield = Instantiate(battlefieldPrefab, placementPose.position, placementPose.rotation);
            }
            else
            {
                // Si ya existía (ej. reubicación), movemos el existente
                spawnedBattlefield.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
                spawnedBattlefield.SetActive(true);
            }

            // Ocultar retícula
            if (placementReticle != null)
            {
                placementReticle.SetActive(false);
            }

            // Ocultar mallas de planos para limpiar la vista
            if (hidePlanesAfterPlacement && planeManager != null)
            {
                SetAllPlanesActive(false);
            }

            // Vincular automáticamente waypoints, contenedor de enemigos y vías de montaña rusa al WaveSpawner y UltimateController
            BindBattlefieldComponents(spawnedBattlefield);

            // Notificar al GameManager que el escenario está colocado y el juego comienza
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Playing);
            }

            Debug.Log("[ARPlacementController] Campo de batalla colocado con éxito en la superficie AR.");
        }

        private void BindBattlefieldComponents(GameObject bf)
        {
            if (bf == null) return;

            Transform pathContainer = bf.transform.Find("Path");
            Transform enemiesContainer = bf.transform.Find("EnemiesContainer");
            if (WaveSpawner.Instance != null && pathContainer != null)
            {
                WaveSpawner.Instance.SetupPathAndContainers(pathContainer, enemiesContainer);
            }

            Transform trackContainer = bf.transform.Find("RollerCoasterTrack");
            Transform cartTransform = bf.transform.Find("RollerCoasterCart");
            if (UltimateController.Instance != null && trackContainer != null && cartTransform != null)
            {
                UltimateController.Instance.SetupTrack(trackContainer, cartTransform.gameObject);
            }
        }

        /// <summary>
        /// Permite reiniciar la colocación si el jugador desea reubicar el campo.
        /// </summary>
        public void ResetPlacement()
        {
            if (spawnedBattlefield != null)
            {
                spawnedBattlefield.SetActive(false);
            }

            if (hidePlanesAfterPlacement && planeManager != null)
            {
                SetAllPlanesActive(true);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Scanning);
            }
        }

        /// <summary>
        /// Activa o desactiva la visualización de los planos detectados.
        /// </summary>
        private void SetAllPlanesActive(bool active)
        {
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Detecta toques en pantalla de forma compatible tanto con el nuevo Input System como con el modo Both/Legacy.
        /// </summary>
        private bool TryGetTouchInput(out Vector2 screenPosition)
        {
            // Detección móvil por toques
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }

            // Detección para pruebas en el editor de Unity mediante ratón
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            screenPosition = default;
            return false;
        }

        /// <summary>
        /// Comprueba si el toque se produjo sobre un elemento de la interfaz de usuario para no confundirlo con una acción en el mundo 3D.
        /// </summary>
        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }
    }
}
