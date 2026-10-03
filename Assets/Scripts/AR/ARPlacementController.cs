using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ConcertDefense.Core;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Escaneo, retícula y colocación del campo (GDD 4.1): Scanning → Placing → Playing.
    /// La retícula sigue el centro de la pantalla con ARRaycastManager contra PlaneWithinPolygon;
    /// un toque fija el prefab Battlefield. Sin AR disponible (editor de Unity o dispositivo sin ARCore)
    /// se activa un modo de prueba con el campo delante de la cámara.
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class ARPlacementController : MonoBehaviour
    {
        public static ARPlacementController Instance { get; private set; }

        [Header("Referencias AR")]
        [SerializeField] private ARRaycastManager raycastManager;
        [Tooltip("Opcional: se usa para ocultar los planos tras colocar el campo.")]
        [SerializeField] private ARPlaneManager planeManager;

        [Header("Colocación")]
        [Tooltip("Retícula que sigue los planos detectados.")]
        [SerializeField] private PlacementReticle placementReticle;

        [Tooltip("Prefab padre del campo de batalla.")]
        [SerializeField] private GameObject battlefieldPrefab;

        [Tooltip("Oculta las mallas de los planos una vez colocado el campo.")]
        [SerializeField] private bool hidePlanesAfterPlacement = true;

        [Header("Modo sin AR (editor / dispositivo no compatible)")]
        [Tooltip("Posición de la cámara respecto al campo cuando no hay AR.")]
        [SerializeField] private Vector3 fallbackCameraOffset = new Vector3(0f, 1.05f, -1.15f);

        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        private GameObject spawnedBattlefield;
        private Pose placementPose;
        private bool placementPoseIsValid;
        private bool fallbackMode;
        private bool fallbackCameraSet;
        private float enabledTime;

        public GameObject SpawnedBattlefield => spawnedBattlefield;
        public bool IsPlaced => spawnedBattlefield != null && spawnedBattlefield.activeSelf;
        public bool HasValidPose => placementPoseIsValid;
        /// <summary>True cuando no hay sesión AR y se juega con la cámara fija de prueba.</summary>
        public bool IsFallbackMode => fallbackMode;

        private void Awake()
        {
            Instance = this;
            if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
            if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
        }

        private void OnEnable()
        {
            enabledTime = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (placementReticle != null) placementReticle.gameObject.SetActive(false);
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.CurrentState == GameState.GameOver || gm.CurrentState == GameState.Victory) return;

            if (IsPlaced)
            {
                if (placementReticle != null && placementReticle.gameObject.activeSelf)
                {
                    placementReticle.gameObject.SetActive(false);
                }
                if (hidePlanesAfterPlacement) SetPlanesVisible(false);
                return;
            }

            UpdatePlacementPose();
            UpdateReticle();

            gm.ChangeState(placementPoseIsValid ? GameState.Placing : GameState.Scanning);

            if (!placementPoseIsValid) return;

            // Un toque (fuera de la UI) fija el campo. Espacio hace lo mismo en el editor.
            bool tapped = PointerInput.PrimaryDown(out Vector2 screenPosition) && !PointerInput.IsOverUI(screenPosition);
            if (tapped || PointerInput.SpacePressed)
            {
                PlaceBattlefield();
            }
        }

        /// <summary>
        /// Raycast desde el centro de la pantalla contra los planos detectados.
        /// </summary>
        private void UpdatePlacementPose()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                placementPoseIsValid = false;
                return;
            }

            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (raycastManager != null && raycastManager.Raycast(screenCenter, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                fallbackMode = false;
                placementPoseIsValid = true;
                placementPose = new Pose(s_Hits[0].pose.position, YawTowards(cam.transform.forward));
                return;
            }

            if (IsArUnavailable())
            {
                // Modo de prueba: el campo se coloca en el origen y la cámara lo mira desde arriba
                fallbackMode = true;
                placementPoseIsValid = true;
                placementPose = new Pose(Vector3.zero, Quaternion.identity);
                SetupFallbackCamera(cam);
                return;
            }

            // AR activo pero sin superficie bajo el centro: retícula roja flotando delante de la cámara
            fallbackMode = false;
            placementPoseIsValid = false;
            Vector3 ahead = cam.transform.position + cam.transform.forward * 1.2f;
            ahead.y = cam.transform.position.y - 0.5f;
            placementPose = new Pose(ahead, YawTowards(cam.transform.forward));
        }

        private static Quaternion YawTowards(Vector3 forward)
        {
            var bearing = new Vector3(forward.x, 0f, forward.z);
            return bearing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(bearing.normalized, Vector3.up) : Quaternion.identity;
        }

        /// <summary>
        /// No hay AR si el dispositivo no es compatible o si, en el editor, la sesión no llega a arrancar.
        /// </summary>
        private bool IsArUnavailable()
        {
            ARSessionState state = ARSession.state;
            if (state == ARSessionState.Unsupported) return true;

            bool sessionRunning = state == ARSessionState.Ready ||
                                  state == ARSessionState.SessionInitializing ||
                                  state == ARSessionState.SessionTracking;

            return Application.isEditor && !sessionRunning && Time.unscaledTime - enabledTime > 1f;
        }

        private void SetupFallbackCamera(Camera cam)
        {
            if (fallbackCameraSet) return;
            fallbackCameraSet = true;

            cam.transform.position = fallbackCameraOffset;
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.02f, 0.05f) - fallbackCameraOffset, Vector3.up);
        }

        private void UpdateReticle()
        {
            if (placementReticle == null) return;

            if (!placementReticle.gameObject.activeSelf) placementReticle.gameObject.SetActive(true);
            placementReticle.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
            placementReticle.SetValid(placementPoseIsValid);
        }

        /// <summary>
        /// Coloca (o reubica) el campo en la pose actual de la retícula. Devuelve false si no hay punto válido.
        /// </summary>
        public bool PlaceBattlefield()
        {
            if (!placementPoseIsValid) return false;

            if (battlefieldPrefab == null)
            {
                Debug.LogError("[ARPlacementController] Falta asignar el prefab Battlefield.");
                return false;
            }

            if (spawnedBattlefield == null)
            {
                spawnedBattlefield = Instantiate(battlefieldPrefab, placementPose.position, placementPose.rotation);
            }
            else
            {
                spawnedBattlefield.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
                spawnedBattlefield.SetActive(true);
            }

            if (placementReticle != null) placementReticle.gameObject.SetActive(false);
            if (hidePlanesAfterPlacement) SetPlanesVisible(false);

            if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Playing);
            GameMessages.Show("¡Escenario colocado! Toca una plataforma para construir y pulsa INICIAR OLEADA.", 4f);
            return true;
        }

        /// <summary>
        /// Vuelve al modo de colocación para reubicar el campo (solo entre oleadas).
        /// </summary>
        public void ResetPlacement()
        {
            if (WaveSpawner.Instance != null && WaveSpawner.Instance.IsWaveInProgress) return;
            if (spawnedBattlefield == null) return;

            spawnedBattlefield.SetActive(false);
            if (hidePlanesAfterPlacement) SetPlanesVisible(true);
            if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Scanning);
        }

        private void SetPlanesVisible(bool visible)
        {
            if (planeManager == null) return;

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.gameObject.activeSelf != visible) plane.gameObject.SetActive(visible);
            }
        }
    }
}
