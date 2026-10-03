using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using ConcertDefense.Core;
using ConcertDefense.Towers;

namespace ConcertDefense.Player
{
    /// <summary>
    /// Controlador de la heroína principal (avatar del jugador):
    /// - Se desplaza hacia el punto tocado en el campo holográfico usando MoveTowards (sin NavMesh).
    /// - Soporta teletransporte instantáneo entre TeleportPads con destello cian.
    /// - Afina y reactiva automáticamente torres silenciadas cuando se aproxima a ellas (mecánica del Jefe Feedback).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AvatarController : MonoBehaviour
    {
        public static AvatarController Instance { get; private set; }

        [Header("Parámetros de Movimiento (GDD 4.2)")]
        [Tooltip("Velocidad de caminata del avatar.")]
        [SerializeField] private float moveSpeed = 1.6f;

        [Tooltip("Velocidad de rotación al orientarse hacia el destino.")]
        [SerializeField] private float rotationSpeed = 14f;

        [Tooltip("Distancia de parada respecto al punto objetivo.")]
        [SerializeField] private float stoppingDistance = 0.05f;

        [Header("Detección de Toques")]
        [Tooltip("Etiqueta que identifica el plano o suelo del campo de batalla.")]
        [SerializeField] private string fieldTag = "Field";

        [Header("Animación y Efectos")]
        [Tooltip("Componente Animator opcional para transiciones de Idle y Walk.")]
        [SerializeField] private Animator animator;

        [Tooltip("Nombre del parámetro booleano en el Animator que indica si camina.")]
        [SerializeField] private string movingAnimParam = "IsMoving";

        [Tooltip("Prefab de partículas instanciado en el punto de destino al tocar el suelo.")]
        [SerializeField] private GameObject moveIndicatorPrefab;

        [Tooltip("Prefab de partículas con destello cian para el teletransporte.")]
        [SerializeField] private GameObject teleportVfxPrefab;

        [Header("Afinación de Torres (Jefe Feedback)")]
        [Tooltip("Distancia a la que el avatar afina automáticamente una torre silenciada.")]
        [SerializeField] private float tuneProximity = 1.0f;

        // Variables de estado
        private Vector3 targetPosition;
        private bool isMoving = false;
        private Camera mainCamera;

        public bool IsMoving => isMoving;
        public Vector3 CurrentDestination => targetPosition;

        // Eventos
        public event Action<Vector3> OnDestinationSet;
        public event Action OnArrivedAtDestination;
        public event Action<Vector3> OnTeleported;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            targetPosition = transform.position;
            mainCamera = Camera.main;
        }

        private void Start()
        {
            // Comprobación periódica de afinación de torres cercanas
            InvokeRepeating(nameof(CheckForSilencedTowersNearby), 0.5f, 0.3f);
        }

        private void Update()
        {
            // Solo procesar movimiento durante el estado de juego activo
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            // 1. Detectar toque en el suelo del campo para fijar destino
            HandleInput();

            // 2. Desplazar avatar hacia el destino mediante MoveTowards
            MoveTowardsTarget();
        }

        /// <summary>
        /// Detecta clics o toques sobre la superficie del campo para mover a la heroína.
        /// </summary>
        private void HandleInput()
        {
            if (TryGetScreenTouch(out Vector2 screenPos))
            {
                if (IsPointerOverUI(screenPos)) return;

                if (mainCamera == null) mainCamera = Camera.main;
                if (mainCamera == null) return;

                Ray ray = mainCamera.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // Si tocamos el suelo del campo (Battlefield o Field)
                    if (hit.collider.CompareTag(fieldTag))
                    {
                        SetDestination(hit.point);
                    }
                }
            }
        }

        /// <summary>
        /// Asigna una nueva posición objetivo y actualiza orientación y animaciones.
        /// </summary>
        public void SetDestination(Vector3 destination)
        {
            // Mantener la misma altura Y que el avatar para no hundirse en el suelo
            destination.y = transform.position.y;
            targetPosition = destination;
            isMoving = true;

            // Instanciar indicador visual en el punto tocado
            if (moveIndicatorPrefab != null)
            {
                Instantiate(moveIndicatorPrefab, targetPosition, Quaternion.identity);
            }

            UpdateAnimator(true);
            OnDestinationSet?.Invoke(targetPosition);
        }

        /// <summary>
        /// Traslada y rota a la heroína suavemente hacia el objetivo sin NavMesh.
        /// </summary>
        private void MoveTowardsTarget()
        {
            if (!isMoving) return;

            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance <= stoppingDistance)
            {
                isMoving = false;
                UpdateAnimator(false);
                OnArrivedAtDestination?.Invoke();
                return;
            }

            // Vector de dirección horizontal
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0f;

            // Rotación suave hacia la dirección de avance
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }

            // Desplazamiento lineal
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Teletransporta al avatar inmediatamente a una nueva posición (usado por TeleportPad).
        /// </summary>
        public void Teleport(Vector3 newWorldPosition)
        {
            // Efecto visual en la posición de salida
            if (teleportVfxPrefab != null)
            {
                Instantiate(teleportVfxPrefab, transform.position, Quaternion.identity);
            }

            // Reposicionamiento instantáneo
            newWorldPosition.y = transform.position.y;
            transform.position = newWorldPosition;
            targetPosition = newWorldPosition;
            isMoving = false;
            UpdateAnimator(false);

            // Efecto visual en la posición de llegada
            if (teleportVfxPrefab != null)
            {
                Instantiate(teleportVfxPrefab, transform.position, Quaternion.identity);
            }

            OnTeleported?.Invoke(newWorldPosition);
            Debug.Log($"[AvatarController] Heroína teletransportada a {newWorldPosition}.");
        }

        /// <summary>
        /// Revisa si hay torres silenciadas cercanas y las afina automáticamente (mecánica Feedback).
        /// </summary>
        private void CheckForSilencedTowersNearby()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, tuneProximity);
            foreach (var col in colliders)
            {
                if (col.CompareTag("Tower"))
                {
                    Tower tower = col.GetComponent<Tower>();
                    if (tower != null && tower.IsSilenced)
                    {
                        tower.Tune();
                    }
                }
            }
        }

        private void UpdateAnimator(bool moving)
        {
            if (animator != null && !string.IsNullOrEmpty(movingAnimParam))
            {
                animator.SetBool(movingAnimParam, moving);
            }
        }

        private bool TryGetScreenTouch(out Vector2 screenPosition)
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    screenPosition = t.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            screenPosition = default;
            return false;
        }

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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, tuneProximity);
        }
    }
}
