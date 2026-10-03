using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Core;

namespace ConcertDefense.Rhythm
{
    /// <summary>
    /// Ataque especial "Roller Coaster" (GDD 4.5):
    /// - El medidor se carga con el combo del botón Beat y, más despacio, con el tiempo (recarga).
    /// - Con el medidor lleno se activa el botón Ultimate.
    /// - El carrito recorre la vía por waypoints y daña a los enemigos que toca.
    /// </summary>
    public class UltimateController : MonoBehaviour
    {
        public static UltimateController Instance { get; private set; }

        [Header("Interfaz")]
        [SerializeField] private Button ultimateButton;

        [Tooltip("Relleno del medidor: se estira de abajo arriba según la carga.")]
        [SerializeField] private RectTransform chargeFill;

        [SerializeField] private TextMeshProUGUI chargeLabel;

        [Tooltip("Resplandor visible cuando el Ultimate está listo.")]
        [SerializeField] private GameObject readyVisualEffect;

        [Header("Montaña Rusa")]
        [Tooltip("Velocidad del carrito en unidades de campo por segundo.")]
        [SerializeField] private float cartSpeed = 1.6f;

        [Tooltip("Daño a cada enemigo que arrolla.")]
        [SerializeField] private float cartDamage = 250f;

        [Tooltip("Velocidad de empuje al arrollar.")]
        [SerializeField] private float knockbackForce = 1.8f;

        [Header("Carga")]
        [Tooltip("Recarga pasiva por segundo durante una oleada (0.012 = lleno en unos 80 s).")]
        [SerializeField] private float passiveChargeRate = 0.012f;

        private float currentCharge;
        private bool isCartActive;

        public float CurrentCharge => currentCharge;
        public bool IsReady => currentCharge >= 1f;
        public bool IsCartActive => isCartActive;

        public event Action<float> OnChargeChanged;
        public event Action OnUltimateReady;
        public event Action OnUltimateExecuted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            RhythmInput.OnUltimateChargeGenerated += AddCharge;
            if (ultimateButton != null) ultimateButton.onClick.AddListener(TriggerUltimate);
            UpdateUI();
        }

        private void OnDestroy()
        {
            RhythmInput.OnUltimateChargeGenerated -= AddCharge;
            if (ultimateButton != null) ultimateButton.onClick.RemoveListener(TriggerUltimate);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            bool waveActive = WaveSpawner.Instance != null && WaveSpawner.Instance.IsWaveInProgress;
            bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;

            if (playing && waveActive && !IsReady && !isCartActive)
            {
                AddCharge(passiveChargeRate * Time.deltaTime);
            }
        }

        /// <summary>
        /// Suma carga al medidor (0–1).
        /// </summary>
        public void AddCharge(float amount)
        {
            if (isCartActive || amount <= 0f) return;

            bool wasReady = IsReady;
            currentCharge = Mathf.Clamp01(currentCharge + amount);

            if (!wasReady && IsReady)
            {
                OnUltimateReady?.Invoke();
                GameMessages.Show("¡Ultimate listo! Pulsa el botón para lanzar el Roller Coaster.");
            }

            OnChargeChanged?.Invoke(currentCharge);
            UpdateUI();
        }

        /// <summary>
        /// Lanza el Roller Coaster si el medidor está lleno.
        /// </summary>
        public void TriggerUltimate()
        {
            if (!IsReady || isCartActive) return;

            Battlefield field = Battlefield.Instance;
            if (field == null || field.RollerCoasterCart == null) return;

            Transform[] track = field.GetTrackWaypoints();
            if (track.Length < 2)
            {
                Debug.LogWarning("[UltimateController] La vía del Roller Coaster no tiene waypoints.");
                return;
            }

            currentCharge = 0f;
            OnChargeChanged?.Invoke(currentCharge);
            OnUltimateExecuted?.Invoke();
            Sfx.Play(SfxId.Ultimate);
            StartCoroutine(CartRoutine(field.RollerCoasterCart, track));
        }

        private IEnumerator CartRoutine(GameObject cart, Transform[] track)
        {
            isCartActive = true;
            UpdateUI();

            // El carrito se mueve en coordenadas locales del campo: sigue funcionando si el campo se escala o rota
            Transform cartTransform = cart.transform;
            cartTransform.localPosition = track[0].localPosition;
            cart.SetActive(true);

            RollerCoasterCart cartComponent = cart.GetComponent<RollerCoasterCart>();
            if (cartComponent != null) cartComponent.Initialize(cartDamage, knockbackForce);

            for (int i = 1; i < track.Length; i++)
            {
                Vector3 target = track[i].localPosition;

                while (cartTransform != null && (cartTransform.localPosition - target).sqrMagnitude > 0.00001f)
                {
                    Vector3 direction = target - cartTransform.localPosition;
                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        Quaternion look = Quaternion.LookRotation(direction.normalized);
                        cartTransform.localRotation = Quaternion.Slerp(cartTransform.localRotation, look, Time.deltaTime * 14f);
                    }

                    cartTransform.localPosition = Vector3.MoveTowards(cartTransform.localPosition, target, cartSpeed * Time.deltaTime);
                    yield return null;
                }

                if (cartTransform == null) break;
            }

            if (cart != null) cart.SetActive(false);
            isCartActive = false;
            UpdateUI();
        }

        private void UpdateUI()
        {
            bool ready = IsReady && !isCartActive;

            if (chargeFill != null)
            {
                chargeFill.anchorMin = Vector2.zero;
                chargeFill.anchorMax = new Vector2(1f, currentCharge);
                chargeFill.offsetMin = Vector2.zero;
                chargeFill.offsetMax = Vector2.zero;
            }

            if (chargeLabel != null)
            {
                chargeLabel.text = isCartActive ? "ROLLER\nCOASTER" : (ready ? "ULTIMATE\n¡LISTO!" : $"ULTIMATE\n{Mathf.FloorToInt(currentCharge * 100f)}%");
            }

            if (ultimateButton != null) ultimateButton.interactable = ready;
            if (readyVisualEffect != null) readyVisualEffect.SetActive(ready);
        }
    }
}
