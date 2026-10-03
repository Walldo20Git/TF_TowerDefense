using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Iluminación según el entorno real (GDD 4.8): pide a ARCore la estimación de luz y la aplica
    /// a la Directional Light en cada frameReceived. También avisa cuando el ambiente es oscuro (modo noche).
    /// </summary>
    public class LightEstimationController : MonoBehaviour
    {
        [Header("Referencias AR")]
        [Tooltip("ARCameraManager de la Main Camera.")]
        [SerializeField] private ARCameraManager cameraManager;

        [Tooltip("Luz direccional a sincronizar (por defecto, la de este objeto).")]
        [SerializeField] private Light directionalLight;

        [Header("Ajustes")]
        [Tooltip("Velocidad de suavizado para evitar parpadeos del sensor.")]
        [SerializeField] private float smoothSpeed = 6f;

        [Tooltip("Rango de intensidad permitido para la luz.")]
        [SerializeField] private Vector2 intensityRange = new Vector2(0.35f, 1.8f);

        [Header("Modo Baja Luz (opcional)")]
        [Tooltip("Brillo (0–1) por debajo del cual el entorno se considera oscuro.")]
        [SerializeField] private float lowLightThreshold = 0.25f;

        private float targetIntensity = 1f;
        private Color targetColor = Color.white;
        private Quaternion targetRotation;
        private bool hasRotation;
        private bool hasEstimate;
        private bool subscribed;

        public float CurrentBrightness { get; private set; } = 1f;

        /// <summary>True si el entorno real está oscuro.</summary>
        public static bool IsLowLight { get; private set; }

        public static event Action<bool> OnLowLightStateChanged;

        private void Awake()
        {
            if (directionalLight == null) directionalLight = GetComponent<Light>();
            IsLowLight = false;

            if (directionalLight != null)
            {
                targetIntensity = directionalLight.intensity;
                targetColor = directionalLight.color;
                targetRotation = directionalLight.transform.rotation;
            }
        }

        private void OnEnable()
        {
            if (cameraManager == null) cameraManager = FindAnyObjectByType<ARCameraManager>();
            if (cameraManager == null) return;

            // Se piden todos los datos; cada dispositivo entrega los que soporta
            cameraManager.requestedLightEstimation =
                LightEstimation.AmbientIntensity |
                LightEstimation.AmbientColor |
                LightEstimation.MainLightDirection |
                LightEstimation.MainLightIntensity;

            cameraManager.frameReceived += OnCameraFrameReceived;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed && cameraManager != null) cameraManager.frameReceived -= OnCameraFrameReceived;
            subscribed = false;
        }

        private void Update()
        {
            if (directionalLight == null || !hasEstimate) return;

            float t = Time.deltaTime * smoothSpeed;
            directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, targetIntensity, t);
            directionalLight.color = Color.Lerp(directionalLight.color, targetColor, t);

            if (hasRotation)
            {
                directionalLight.transform.rotation = Quaternion.Slerp(directionalLight.transform.rotation, targetRotation, t);
            }
        }

        /// <summary>
        /// Se ejecuta con cada fotograma de la cámara AR.
        /// </summary>
        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            ARLightEstimationData estimation = args.lightEstimation;
            float? brightness = null;

            // 1. Brillo: ambiente medio o, en modo HDR, el de la luz principal
            if (estimation.averageBrightness.HasValue) brightness = estimation.averageBrightness.Value;
            else if (estimation.averageMainLightBrightness.HasValue) brightness = estimation.averageMainLightBrightness.Value;

            if (brightness.HasValue)
            {
                hasEstimate = true;
                CurrentBrightness = Mathf.Clamp01(brightness.Value);
                // Un brillo medio de 0.5 equivale a intensidad 1
                targetIntensity = Mathf.Clamp(CurrentBrightness * 2f, intensityRange.x, intensityRange.y);
            }

            // 2. Color
            if (estimation.colorCorrection.HasValue)
            {
                hasEstimate = true;
                targetColor = estimation.colorCorrection.Value;
            }
            else if (estimation.mainLightColor.HasValue)
            {
                hasEstimate = true;
                targetColor = estimation.mainLightColor.Value;
            }

            // 3. Dirección de la luz principal
            if (estimation.mainLightDirection.HasValue)
            {
                hasEstimate = true;
                hasRotation = true;
                targetRotation = Quaternion.LookRotation(estimation.mainLightDirection.Value);
            }

            // 4. Modo noche
            if (brightness.HasValue)
            {
                bool isDark = CurrentBrightness < lowLightThreshold;
                if (isDark != IsLowLight)
                {
                    IsLowLight = isDark;
                    OnLowLightStateChanged?.Invoke(IsLowLight);
                }
            }
        }
    }
}
