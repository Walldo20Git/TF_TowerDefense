using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ConcertDefense.Core;
using ConcertDefense.Player;
using ConcertDefense.Towers;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Manipulación táctil del campo (GDD 4.7):
    /// - Pellizco: escalar entre 0.5× y 2×.
    /// - Giro con dos dedos: rotar en Y.
    /// - Arrastre con un dedo sobre una zona libre: mover el campo.
    /// Solo con la oleada detenida, para no confundirlo con los toques de combate.
    /// </summary>
    public class FieldManipulator : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Para proyectar el arrastre sobre los planos detectados.")]
        [SerializeField] private ARRaycastManager raycastManager;

        [Header("Límites de Escala (GDD 4.7)")]
        [SerializeField] private float minScaleMultiplier = 0.5f;
        [SerializeField] private float maxScaleMultiplier = 2f;

        [Header("Sensibilidad")]
        [Tooltip("Píxeles que debe moverse el dedo para que el toque cuente como arrastre.")]
        [SerializeField] private float dragThresholdPixels = 28f;

        [Header("Control de Estado")]
        [Tooltip("Interruptor general de la manipulación.")]
        [SerializeField] private bool manipulationAllowed = true;

        public bool ManipulationAllowed
        {
            get => manipulationAllowed;
            set => manipulationAllowed = value;
        }

        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        private static readonly RaycastHit[] s_PhysicsHits = new RaycastHit[16];

        private Transform field;
        private Vector3 baseScale = Vector3.one;
        private float scaleFactor = 1f;

        private bool twoFingerActive;
        private float previousDistance;
        private Vector2 previousVector;

        private bool dragCandidate;
        private bool dragging;
        private Vector2 dragStartScreen;
        private Vector3 grabOffset;

        private void Awake()
        {
            if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
        }

        private void Update()
        {
            if (!CanManipulate())
            {
                twoFingerActive = false;
                dragCandidate = false;
                dragging = false;
                return;
            }

            // Si el campo cambió (primera colocación), se toma su escala como 1×
            Transform current = Battlefield.Instance.transform;
            if (current != field)
            {
                field = current;
                baseScale = field.localScale;
                scaleFactor = 1f;
            }

            if (PointerInput.PressedCount >= 2)
            {
                dragCandidate = false;
                dragging = false;
                HandleTwoFingers();
                return;
            }

            twoFingerActive = false;
            HandleOneFingerDrag();
            HandleEditorMouse();
        }

        private bool CanManipulate()
        {
            if (!manipulationAllowed) return false;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
            if (Battlefield.Instance == null) return false;
            if (WaveSpawner.Instance != null && WaveSpawner.Instance.IsWaveInProgress) return false;
            return true;
        }

        /// <summary>
        /// Pellizco para escalar y giro para rotar en Y, a la vez.
        /// </summary>
        private void HandleTwoFingers()
        {
            if (!PointerInput.TryGetPressedTouch(0, out Vector2 p0) || !PointerInput.TryGetPressedTouch(1, out Vector2 p1)) return;
            if (PointerInput.IsOverUI(p0) || PointerInput.IsOverUI(p1)) return;

            Vector2 vector = p1 - p0;
            float distance = vector.magnitude;

            if (!twoFingerActive)
            {
                twoFingerActive = true;
                previousDistance = distance;
                previousVector = vector;
                return;
            }

            if (previousDistance > 1f)
            {
                SetScaleFactor(scaleFactor * (distance / previousDistance));
            }

            float angle = Vector2.SignedAngle(previousVector, vector);
            field.Rotate(Vector3.up, -angle, Space.World);

            previousDistance = distance;
            previousVector = vector;
        }

        /// <summary>
        /// Arrastre con un dedo: empieza en zona libre y solo cuenta al superar un umbral
        /// (un toque corto sigue sirviendo para mover a la heroína).
        /// </summary>
        private void HandleOneFingerDrag()
        {
            if (PointerInput.PrimaryDown(out Vector2 downPos))
            {
                dragging = false;
                dragStartScreen = downPos;
                dragCandidate = !PointerInput.IsOverUI(downPos) && !HitsInteractive(downPos);
                return;
            }

            if (!dragCandidate || !PointerInput.PrimaryHeld(out Vector2 pos))
            {
                dragCandidate = false;
                dragging = false;
                return;
            }

            if (!dragging)
            {
                if ((pos - dragStartScreen).magnitude < dragThresholdPixels) return;
                if (!TryGetSurfacePoint(dragStartScreen, out Vector3 grabPoint)) return;

                dragging = true;
                grabOffset = field.position - grabPoint;
            }

            if (TryGetSurfacePoint(pos, out Vector3 point))
            {
                field.position = point + grabOffset;
            }
        }

        /// <summary>
        /// Pruebas en el editor: rueda del ratón para escalar y clic derecho para rotar.
        /// </summary>
        private void HandleEditorMouse()
        {
            float scroll = PointerInput.ScrollDelta;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                SetScaleFactor(scaleFactor * (1f + Mathf.Sign(scroll) * 0.08f));
            }

            if (PointerInput.SecondaryHeld)
            {
                field.Rotate(Vector3.up, -PointerInput.MouseDelta.x * 0.4f, Space.World);
            }
        }

        private void SetScaleFactor(float factor)
        {
            scaleFactor = Mathf.Clamp(factor, minScaleMultiplier, maxScaleMultiplier);
            field.localScale = baseScale * scaleFactor;
        }

        /// <summary>
        /// ¿El toque cae sobre una torre, plataforma, pad o la heroína? Entonces no es arrastre de campo.
        /// </summary>
        private static bool HitsInteractive(Vector2 screenPosition)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            int count = Physics.RaycastNonAlloc(cam.ScreenPointToRay(screenPosition), s_PhysicsHits, 50f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider col = s_PhysicsHits[i].collider;
                if (col.GetComponentInParent<Tower>() != null) return true;
                if (col.GetComponentInParent<BuildSpot>() != null) return true;
                if (col.GetComponentInParent<TeleportPad>() != null) return true;
                if (col.GetComponentInParent<AvatarController>() != null) return true;
            }
            return false;
        }

        /// <summary>
        /// Punto de la superficie real bajo el dedo; si no hay plano AR, el plano horizontal a la altura del campo.
        /// </summary>
        private bool TryGetSurfacePoint(Vector2 screenPosition, out Vector3 point)
        {
            if (raycastManager != null && raycastManager.Raycast(screenPosition, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                point = s_Hits[0].pose.position;
                return true;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                var plane = new Plane(Vector3.up, field.position);
                Ray ray = cam.ScreenPointToRay(screenPosition);
                if (plane.Raycast(ray, out float enter))
                {
                    point = ray.GetPoint(enter);
                    return true;
                }
            }

            point = default;
            return false;
        }
    }
}
