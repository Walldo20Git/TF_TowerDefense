using UnityEngine;

namespace ConcertDefense.UI
{
    /// <summary>
    /// Gestiona elementos de interfaz en World Space (barras de vida, menús flotantes e indicadores):
    /// - Sigue la posición de un objeto 3D objetivo con un desplazamiento (offset).
    /// - Orienta el Canvas hacia la cámara AR en tiempo real (efecto Billboard).
    /// - Opcionalmente compensa la escala con la distancia para mantener legibilidad visual en la mesa AR.
    /// - Se destruye automáticamente si el objetivo seguido es eliminado.
    /// </summary>
    public class UIFollow : MonoBehaviour
    {
        [Header("Seguimiento de Objetivo")]
        [Tooltip("Transform del objeto en el mundo real/virtual a seguir.")]
        [SerializeField] private Transform targetTransform;

        [Tooltip("Desplazamiento vertical y horizontal relativo al objetivo.")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

        [Header("Comportamiento Billboard")]
        [Tooltip("Si es true, solo rota en el eje Y. Si es false (recomendado en AR), mira de frente a la cámara en todos los ejes.")]
        [SerializeField] private bool lockYAxisOnly = false;

        [Header("Compensación de Tamaño con Distancia (Opcional)")]
        [Tooltip("Si es true, ajusta la escala según la distancia del jugador para mantener un tamaño aparente legible.")]
        [SerializeField] private bool keepConstantScreenSize = false;

        [Tooltip("Factor multiplicador de escala con la distancia.")]
        [SerializeField] private float sizeMultiplier = 0.002f;

        [Tooltip("Escala mínima permitida.")]
        [SerializeField] private float minScale = 0.001f;

        [Tooltip("Escala máxima permitida.")]
        [SerializeField] private float maxScale = 0.006f;

        [Header("Ciclo de Vida")]
        [Tooltip("Si es true, este Canvas se destruye cuando el objetivo seguido deja de existir.")]
        [SerializeField] private bool destroyWithTarget = true;

        private Camera mainCamera;
        private bool hasInitializedTarget = false;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Start()
        {
            if (targetTransform != null)
            {
                hasInitializedTarget = true;
            }
        }

        /// <summary>
        /// Asigna dinámicamente el objetivo a seguir y su desplazamiento.
        /// </summary>
        public void SetTarget(Transform target, Vector3? customOffset = null)
        {
            targetTransform = target;
            hasInitializedTarget = target != null;

            if (customOffset.HasValue)
            {
                worldOffset = customOffset.Value;
            }

            // Actualizar inmediatamente posición para evitar saltos de fotograma
            if (targetTransform != null)
            {
                transform.position = targetTransform.position + worldOffset;
            }
        }

        private void LateUpdate()
        {
            // Si el objetivo original fue destruido y destroyWithTarget está activo, eliminamos el Canvas
            if (hasInitializedTarget && targetTransform == null)
            {
                if (destroyWithTarget)
                {
                    Destroy(gameObject);
                }
                return;
            }

            if (targetTransform != null)
            {
                transform.position = targetTransform.position + worldOffset;
            }

            // Actualizar orientación Billboard hacia la cámara AR
            UpdateBillboardRotation();

            // Ajustar escala dinámica si está habilitado
            if (keepConstantScreenSize)
            {
                UpdateDistanceScaling();
            }
        }

        /// <summary>
        /// Rota el Canvas para mirar en todo momento hacia la cámara AR del jugador.
        /// </summary>
        private void UpdateBillboardRotation()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            if (lockYAxisOnly)
            {
                Vector3 lookDirection = mainCamera.transform.position - transform.position;
                lookDirection.y = 0f;
                if (lookDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(-lookDirection);
                }
            }
            else
            {
                // En AR, al observar una mesa desde arriba, la rotación directa de la cámara da el mejor resultado
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        /// <summary>
        /// Adapta la escala del Canvas en función de lo cerca o lejos que esté el teléfono del objeto.
        /// </summary>
        private void UpdateDistanceScaling()
        {
            if (mainCamera == null) return;

            float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
            float calculatedScale = Mathf.Clamp(distance * sizeMultiplier, minScale, maxScale);
            transform.localScale = Vector3.one * calculatedScale;
        }
    }
}
