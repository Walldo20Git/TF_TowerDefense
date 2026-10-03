using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.UI
{
    /// <summary>
    /// UI que sigue a un objeto en el espacio real (requisito del caso práctico):
    /// - Sigue la posición de un objeto 3D con un desplazamiento.
    /// - Orienta el Canvas World Space hacia la cámara AR (billboard).
    /// - Opcionalmente ajusta la escala con la distancia para que siga siendo legible.
    /// </summary>
    public class UIFollow : MonoBehaviour
    {
        [Header("Seguimiento")]
        [Tooltip("Objeto a seguir.")]
        [SerializeField] private Transform targetTransform;

        [Tooltip("Desplazamiento sobre el objetivo, en unidades de campo.")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.2f, 0f);

        [Header("Billboard")]
        [Tooltip("Si es true solo gira en el eje Y; si es false mira de frente a la cámara.")]
        [SerializeField] private bool lockYAxisOnly = false;

        [Header("Tamaño con la distancia (opcional)")]
        [Tooltip("Mantiene un tamaño aparente constante en pantalla.")]
        [SerializeField] private bool keepConstantScreenSize = false;

        [Tooltip("Escala por cada metro de distancia a la cámara.")]
        [SerializeField] private float sizeMultiplier = 0.0007f;

        [SerializeField] private float minScale = 0.0003f;
        [SerializeField] private float maxScale = 0.004f;

        [Header("Ciclo de Vida")]
        [Tooltip("Destruir este Canvas cuando el objetivo deja de existir.")]
        [SerializeField] private bool destroyWithTarget = true;

        private Camera mainCamera;
        private bool hadTarget;

        /// <summary>
        /// Asigna el objetivo y, opcionalmente, el desplazamiento.
        /// </summary>
        public void SetTarget(Transform target, Vector3? customOffset = null)
        {
            targetTransform = target;
            hadTarget = target != null;
            if (customOffset.HasValue) worldOffset = customOffset.Value;

            LateUpdate();
        }

        private void LateUpdate()
        {
            if (targetTransform == null)
            {
                if (hadTarget && destroyWithTarget) Destroy(gameObject);
                return;
            }

            hadTarget = true;
            transform.position = targetTransform.position + worldOffset * Battlefield.Scale;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            if (lockYAxisOnly)
            {
                Vector3 toCamera = mainCamera.transform.position - transform.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-toCamera);
            }
            else
            {
                // En AR, mirando una mesa desde arriba, copiar la rotación de la cámara da el mejor resultado
                transform.rotation = mainCamera.transform.rotation;
            }

            if (keepConstantScreenSize)
            {
                float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
                transform.localScale = Vector3.one * Mathf.Clamp(distance * sizeMultiplier, minScale, maxScale);
            }
        }
    }
}
