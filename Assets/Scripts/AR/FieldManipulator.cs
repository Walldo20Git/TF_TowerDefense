using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ConcertDefense.Core;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Gestiona la manipulación táctil del campo de batalla en AR:
    /// - Pellizco con dos dedos: escalar entre 0.5x y 2x.
    /// - Giro con dos dedos: rotar sobre el eje Y.
    /// - Arrastre con un dedo en área libre: reposicionar sobre la superficie AR.
    /// Solo se permite manipular cuando no hay una oleada activa para no interferir con el combate.
    /// </summary>
    public class FieldManipulator : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Controlador de colocación para obtener la referencia del Battlefield instanciado.")]
        [SerializeField] private ARPlacementController placementController;

        [Tooltip("Gestor de raycasts de AR Foundation para proyectar el arrastre sobre planos.")]
        [SerializeField] private ARRaycastManager raycastManager;

        [Header("Límites de Escala")]
        [Tooltip("Multiplicador de escala mínima (0.5x según GDD).")]
        [SerializeField] private float minScaleMultiplier = 0.5f;

        [Tooltip("Multiplicador de escala máxima (2.0x según GDD).")]
        [SerializeField] private float maxScaleMultiplier = 2.0f;

        [Header("Sensibilidad")]
        [Tooltip("Sensibilidad del gesto de pellizco.")]
        [SerializeField] private float pinchSensitivity = 0.003f;

        [Tooltip("Sensibilidad de rotación con dos dedos.")]
        [SerializeField] private float rotationSensitivity = 0.4f;

        [Header("Control de Estado")]
        [Tooltip("Permite habilitar o deshabilitar la manipulación según si hay una oleada en curso.")]
        [SerializeField] private bool manipulationAllowed = true;

        public bool ManipulationAllowed
        {
            get => manipulationAllowed;
            set => manipulationAllowed = value;
        }

        private Transform targetFieldTransform;
        private Vector3 baseScale = Vector3.one;
        private float currentScaleFactor = 1.0f;

        // Variables de seguimiento de gestos
        private float previousTouchDistance;
        private Vector2 previousTouchVector;
        private bool isTwoFingerGestureActive = false;
        private bool isDraggingField = false;

        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        private void Awake()
        {
            if (placementController == null)
            {
                placementController = FindFirstObjectByType<ARPlacementController>();
            }

            if (raycastManager == null)
            {
                raycastManager = GetComponent<ARRaycastManager>();
                if (raycastManager == null)
                {
                    raycastManager = FindFirstObjectByType<ARRaycastManager>();
                }
            }
        }

        private void Update()
        {
            // Solo actuar si el juego está en estado Playing y se permite manipular
            if (!manipulationAllowed || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            // Obtener el transform del campo si aún no lo tenemos
            if (targetFieldTransform == null)
            {
                if (placementController != null && placementController.SpawnedBattlefield != null)
                {
                    targetFieldTransform = placementController.SpawnedBattlefield.transform;
                    baseScale = targetFieldTransform.localScale;
                }
                else
                {
                    return;
                }
            }

            // 1. Manejo táctil con dos dedos (Escala + Rotación)
            if (Input.touchCount == 2)
            {
                HandleTwoFingerGestures();
                return;
            }
            else
            {
                isTwoFingerGestureActive = false;
            }

            // 2. Manejo táctil con un dedo (Arrastre sobre superficie AR)
            if (Input.touchCount == 1)
            {
                HandleOneFingerDrag();
                return;
            }
            else
            {
                isDraggingField = false;
            }

            // 3. Simulación para pruebas rápidas dentro del Editor de Unity
            #if UNITY_EDITOR
            HandleEditorSimulation();
            #endif
        }

        /// <summary>
        /// Procesa simultáneamente el pellizco para escalar y el giro angular para rotar en Y.
        /// </summary>
        private void HandleTwoFingerGestures()
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // Ignorar si alguno de los dedos interactúa sobre un botón de la UI
            if (IsPointerOverUI(touch0.position) || IsPointerOverUI(touch1.position))
            {
                return;
            }

            Vector2 currentTouchVector = touch1.position - touch0.position;
            float currentDistance = currentTouchVector.magnitude;

            if (!isTwoFingerGestureActive)
            {
                previousTouchDistance = currentDistance;
                previousTouchVector = currentTouchVector;
                isTwoFingerGestureActive = true;
                return;
            }

            // --- Escala (Pellizco) ---
            float distanceDelta = currentDistance - previousTouchDistance;
            currentScaleFactor += distanceDelta * pinchSensitivity;
            currentScaleFactor = Mathf.Clamp(currentScaleFactor, minScaleMultiplier, maxScaleMultiplier);
            targetFieldTransform.localScale = baseScale * currentScaleFactor;

            // --- Rotación (Giro de dos dedos) ---
            float angleDelta = Vector2.SignedAngle(previousTouchVector, currentTouchVector);
            targetFieldTransform.Rotate(Vector3.up, -angleDelta * rotationSensitivity, Space.World);

            previousTouchDistance = currentDistance;
            previousTouchVector = currentTouchVector;
        }

        /// <summary>
        /// Permite arrastrar el campo sobre planos detectados tocando una zona libre.
        /// </summary>
        private void HandleOneFingerDrag()
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (IsPointerOverUI(touch.position))
                {
                    isDraggingField = false;
                    return;
                }

                // Verificar si el toque no impacta directamente sobre una torre o interactuable
                if (Physics.Raycast(Camera.main.ScreenPointToRay(touch.position), out RaycastHit hit))
                {
                    // Si tocamos una torre o botón de construcción, no iniciamos arrastre de campo
                    if (hit.collider.CompareTag("Tower") || hit.collider.CompareTag("BuildSpot") || hit.collider.CompareTag("Avatar"))
                    {
                        isDraggingField = false;
                        return;
                    }
                }

                isDraggingField = true;
            }

            if (isDraggingField && touch.phase == TouchPhase.Moved && raycastManager != null)
            {
                if (raycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = s_Hits[0].pose;
                    targetFieldTransform.position = hitPose.position;
                }
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDraggingField = false;
            }
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Soporte de depuración en el editor de Unity mediante ratón y rueda de desplazamiento.
        /// </summary>
        private void HandleEditorSimulation()
        {
            // Rueda del ratón para escalar
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentScaleFactor += scroll * 2f;
                currentScaleFactor = Mathf.Clamp(currentScaleFactor, minScaleMultiplier, maxScaleMultiplier);
                targetFieldTransform.localScale = baseScale * currentScaleFactor;
            }

            // Clic derecho sostenido para rotar en Y
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X");
                targetFieldTransform.Rotate(Vector3.up, -mouseX * 5f, Space.World);
            }
        }
        #endif

        /// <summary>
        /// Comprueba si la posición en pantalla coincide con un elemento de UI.
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
