using System;
using UnityEngine;
using UnityEngine.EventSystems;
using ConcertDefense.Core;

namespace ConcertDefense.Player
{
    /// <summary>
    /// Plataforma de teletransporte distribuida por el campo:
    /// - Al tocar un pad, la heroína se traslada a su posición con un destello cian.
    /// - Si la heroína ya está en este pad y existe un pairedPad asignado, se teletransporta hacia el pad emparejado.
    /// - Posee tiempo de enfriamiento para prevenir rebotes accidentales.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TeleportPad : MonoBehaviour
    {
        [Header("Conexión y Destino")]
        [Tooltip("Pad emparejado hacia el cual enviar al avatar si ya está parado en este pad.")]
        [SerializeField] private TeleportPad pairedPad;

        [Tooltip("Punto de aterrizaje exacto del avatar (si es nulo, usa la posición del pad).")]
        [SerializeField] private Transform arrivalPoint;

        [Header("Efectos (GDD 4.2: Destello Cian)")]
        [Tooltip("Prefab de partículas con destello de luz cian.")]
        [SerializeField] private GameObject cyanFlashVfxPrefab;

        [Tooltip("Sonido de teletransporte espacial/anime.")]
        [SerializeField] private AudioClip teleportSfx;

        [Header("Ajustes")]
        [Tooltip("Tiempo en segundos antes de que el pad pueda volver a activarse.")]
        [SerializeField] private float cooldown = 1.0f;

        // Variables de estado
        private float lastTeleportTime = -10f;
        private AudioSource audioSource;

        public Vector3 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position;
        public bool IsOnCooldown => Time.time < lastTeleportTime + cooldown;

        // Eventos
        public static event Action<TeleportPad> OnTeleportTriggered;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && teleportSfx != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.8f; // Audio 3D moderado
            }

            if (arrivalPoint == null)
            {
                arrivalPoint = transform;
            }
        }

        private void OnMouseDown()
        {
            // Ignorar clic si el cursor está sobre un elemento de UI (ej: botón Iniciar Oleada)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Solo actuar durante partida en curso
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            if (IsOnCooldown) return;

            ActivatePad();
        }

        /// <summary>
        /// Activa la lógica de teletransporte según la ubicación actual del avatar.
        /// </summary>
        public void ActivatePad()
        {
            AvatarController avatar = AvatarController.Instance;
            if (avatar == null)
            {
                avatar = FindFirstObjectByType<AvatarController>();
                if (avatar == null) return;
            }

            float distanceToAvatar = Vector3.Distance(avatar.transform.position, ArrivalPosition);

            // Si el avatar ya está en este pad y tenemos un pad emparejado, enviarlo al pad compañero
            if (distanceToAvatar < 0.5f && pairedPad != null)
            {
                TeleportAvatarTo(pairedPad.ArrivalPosition);
                pairedPad.RegisterTeleportArrival();
            }
            else
            {
                // Si el avatar está lejos, traerlo a este pad
                TeleportAvatarTo(ArrivalPosition);
            }

            RegisterTeleportArrival();
        }

        /// <summary>
        /// Ejecuta el traslado instantáneo, reproduce efectos y emite eventos.
        /// </summary>
        private void TeleportAvatarTo(Vector3 destination)
        {
            AvatarController avatar = AvatarController.Instance;
            if (avatar != null)
            {
                // Destello cian de salida
                SpawnFlashVfx(avatar.transform.position);

                // Teletransporte
                avatar.Teleport(destination);

                // Destello cian de llegada
                SpawnFlashVfx(destination);

                // Sonido
                PlayTeleportSound();

                OnTeleportTriggered?.Invoke(this);
            }
        }

        /// <summary>
        /// Instancia el efecto visual de partículas de destello cian.
        /// </summary>
        private void SpawnFlashVfx(Vector3 pos)
        {
            if (cyanFlashVfxPrefab != null)
            {
                Instantiate(cyanFlashVfxPrefab, pos, Quaternion.identity);
            }
        }

        private void PlayTeleportSound()
        {
            if (audioSource != null && teleportSfx != null)
            {
                audioSource.PlayOneShot(teleportSfx);
            }
        }

        /// <summary>
        /// Registra el tiempo para el enfriamiento del pad.
        /// </summary>
        public void RegisterTeleportArrival()
        {
            lastTeleportTime = Time.time;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Opcional: si el avatar camina físicamente hacia el pad y hay un pad compañero configurado
            if (other.CompareTag("Avatar") && !IsOnCooldown && pairedPad != null)
            {
                ActivatePad();
            }
        }
    }
}
