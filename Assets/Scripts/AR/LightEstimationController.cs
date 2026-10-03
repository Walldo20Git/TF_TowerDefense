using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Sincroniza la iluminación virtual (Directional Light) con la estimación de luz del entorno real proporcionada por ARCore.
    /// También detecta condiciones de baja luminosidad para posible modo nocturno/neón de las torres.
    /// </summary>
    public class LightEstimationController : MonoBehaviour
    {
        [Header("Referencias AR")]
        [Tooltip("Gestor de la cámara AR donde se reciben los fotogramas y la estimación de luz.")]
        [SerializeField] private ARCameraManager cameraManager;

        [Tooltip("Luz direccional principal de la escena a sincronizar.")]
        [SerializeField] private Light directionalLight;

        [Header("Ajustes de Suavizado")]
        [Tooltip("Velocidad de transición suave para evitar parpadeos bruscos ante cambios de luz del sensor.")]
        [SerializeField] private float smoothSpeed = 6f;

        [Header("Modo Baja Luz (Opcional)")]
        [Tooltip("Umbral de brillo (0 a 1) por debajo del cual se considera entorno oscuro.")]
        [SerializeField] private float lowLightThreshold = 0.25f;

        // Valores objetivo calculados a partir de los sensores AR
        private float targetIntensity = 1f;
        private Color targetColor = Color.white;
        private Quaternion targetRotation;
        private bool hasRotation = false;

        // Estado público de luminosidad
        public float CurrentBrightness { get; private set; } = 1f;
        public bool IsLowLight { get; private set; } = false;

        // Evento notificado cuando se entra o sale del umbral de baja luz
        public static event Action<bool> OnLowLightStateChanged;

        private void Awake()
        {
            if (directionalLight == null)
            {
                directionalLight = GetComponent<Light>();
            }

            if (cameraManager == null)
            {
                cameraManager = FindFirstObjectByType<ARCameraManager>();
            }

            if (directionalLight != null)
            {
                targetIntensity = directionalLight.intensity;
                targetColor = directionalLight.color;
                targetRotation = directionalLight.transform.rotation;
            }
        }

        private void OnEnable()
        {
            if (cameraManager != null)
            {
                cameraManager.frameReceived += OnCameraFrameReceived;
            }
        }

        private void OnDisable()
        {
            if (cameraManager != null)
            {
                cameraManager.frameReceived -= OnCameraFrameReceived;
            }
        }

        private void Update()
        {
            if (directionalLight == null) return;

            // Interpolación suave de intensidad y color
            directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, targetIntensity, Time.deltaTime * smoothSpeed);
            directionalLight.color = Color.Lerp(directionalLight.color, targetColor, Time.deltaTime * smoothSpeed);

            // Interpolación de dirección si el dispositivo soporta estimación de dirección solar/principal
            if (hasRotation)
            {
                directionalLight.transform.rotation = Quaternion.Slerp(directionalLight.transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
            }
        }

        /// <summary>
        /// Callback ejecutado cada vez que AR Foundation recibe un nuevo fotograma con datos de luz de ARCore.
        /// </summary>
        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            var lightEstimation = args.lightEstimation;

            // 1. Brillo medio / Intensidad lumínica
            if (lightEstimation.averageBrightness.HasValue)
            {
                CurrentBrightness = lightEstimation.averageBrightness.Value;
                targetIntensity = CurrentBrightness;
            }
            else if (lightEstimation.mainLightIntensityLumens.HasValue)
            {
                // Conversión aproximada de lúmenes a escala de intensidad Unity estándar
                CurrentBrightness = Mathf.Clamp01(lightEstimation.mainLightIntensityLumens.Value / 1000f);
                targetIntensity = CurrentBrightness;
            }

            // 2. Corrección de color y balance de blancos
            if (lightEstimation.colorCorrection.HasValue)
            {
                targetColor = lightEstimation.colorCorrection.Value;
            }
            else if (lightEstimation.mainLightColor.HasValue)
            {
                targetColor = lightEstimation.mainLightColor.Value;
            }

            // 3. Dirección de la luz principal
            if (lightEstimation.mainLightDirection.HasValue)
            {
                targetRotation = Quaternion.LookRotation(lightEstimation.mainLightDirection.Value);
                hasRotation = true;
            }

            // 4. Verificación de umbral de baja luz
            bool isDark = CurrentBrightness < lowLightThreshold;
            if (isDark != IsLowLight)
            {
                IsLowLight = isDark;
                OnLowLightStateChanged?.Invoke(IsLowLight);
            }
        }
    }
}
