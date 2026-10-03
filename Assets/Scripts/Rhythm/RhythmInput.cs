using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Towers;

namespace ConcertDefense.Rhythm
{
    /// <summary>
    /// Gestiona la interacción del jugador con el botón rítmico (GDD 4.4):
    /// - Evalúa las pulsaciones contra el BeatClock (Perfect, Good, Miss).
    /// - Mantiene el contador de Combo y actualiza los textos visuales de retroalimentación.
    /// - Aplica el multiplicador de 1.5x de daño a las torres durante el compás al acertar Perfect.
    /// - Genera carga para el Ultimate (Roller Coaster) proporcional al combo y precisión.
    /// - Anima visualmente el botón al pulso del metrónomo.
    /// </summary>
    public class RhythmInput : MonoBehaviour
    {
        public static RhythmInput Instance { get; private set; }

        [Header("Referencias de UI")]
        [Tooltip("Botón principal de ritmo (independiente de los toques sobre el campo 3D).")]
        [SerializeField] private Button beatButton;

        [Tooltip("Texto para mostrar la calificación del acierto (PERFECT / GOOD / MISS).")]
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Tooltip("Texto que muestra la racha de combo actual.")]
        [SerializeField] private TextMeshProUGUI comboText;

        [Tooltip("Transform o imagen de anillo que palpita con el compás de la música.")]
        [SerializeField] private RectTransform pulseRing;

        [Header("Parámetros de Recompensa de Carga Ultimate")]
        [Tooltip("Porcentaje de carga de Ultimate otorgado por un PERFECT (0.05 = 5%).")]
        [SerializeField] private float perfectChargeAmount = 0.06f;

        [Tooltip("Porcentaje de carga de Ultimate otorgado por un GOOD (0.03 = 3%).")]
        [SerializeField] private float goodChargeAmount = 0.03f;

        [Header("Paleta Visual Anime")]
        [SerializeField] private Color perfectColor = new Color(0f, 1f, 0.9f); // Turquesa
        [SerializeField] private Color goodColor = new Color(1f, 0.2f, 0.7f);    // Magenta
        [SerializeField] private Color missColor = new Color(0.6f, 0.6f, 0.6f);  // Gris neutro

        // Variables de estado
        private int currentCombo = 0;
        private int maxCombo = 0;
        private Coroutine feedbackFadeCoroutine;
        private Coroutine rhythmBoostCoroutine;

        public int CurrentCombo => currentCombo;
        public int MaxCombo => maxCombo;

        // Eventos
        public static event Action<HitAccuracy, int> OnRhythmHit;
        public static event Action<float> OnUltimateChargeGenerated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (beatButton != null)
            {
                beatButton.onClick.AddListener(OnBeatPressed);
            }

            if (BeatClock.Instance != null)
            {
                BeatClock.Instance.OnBeatPulse += AnimatePulseRing;
            }
        }

        private void OnDisable()
        {
            if (beatButton != null)
            {
                beatButton.onClick.RemoveListener(OnBeatPressed);
            }

            if (BeatClock.Instance != null)
            {
                BeatClock.Instance.OnBeatPulse -= AnimatePulseRing;
            }
        }

        private void Start()
        {
            UpdateComboUI();

            if (feedbackText != null)
            {
                feedbackText.text = "";
            }
        }

        /// <summary>
        /// Ejecutado cada vez que el usuario presiona el botón Beat en la pantalla.
        /// </summary>
        public void OnBeatPressed()
        {
            if (BeatClock.Instance == null || !BeatClock.Instance.IsRunning) return;

            HitAccuracy accuracy = BeatClock.Instance.EvaluateTap(out float offset);

            switch (accuracy)
            {
                case HitAccuracy.Perfect:
                    currentCombo++;
                    maxCombo = Mathf.Max(maxCombo, currentCombo);
                    ApplyPerfectDamageBoost();
                    AddUltimateCharge(perfectChargeAmount);
                    ShowFeedback("PERFECT!", perfectColor);
                    break;

                case HitAccuracy.Good:
                    currentCombo++;
                    maxCombo = Mathf.Max(maxCombo, currentCombo);
                    AddUltimateCharge(goodChargeAmount);
                    ShowFeedback("GOOD", goodColor);
                    break;

                case HitAccuracy.Miss:
                    currentCombo = 0;
                    ShowFeedback("MISS", missColor);
                    break;
            }

            UpdateComboUI();
            OnRhythmHit?.Invoke(accuracy, currentCombo);
        }

        /// <summary>
        /// Aplica un multiplicador de 1.5x de daño a todas las torres durante el compás actual.
        /// </summary>
        private void ApplyPerfectDamageBoost()
        {
            if (rhythmBoostCoroutine != null)
            {
                StopCoroutine(rhythmBoostCoroutine);
            }

            rhythmBoostCoroutine = StartCoroutine(RhythmBoostRoutine());
        }

        private IEnumerator RhythmBoostRoutine()
        {
            Tower[] allTowers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            foreach (var tower in allTowers)
            {
                tower.SetRhythmMultiplier(1.5f);
            }

            // Mantener el bono durante la duración de un pulso de compás
            float duration = BeatClock.Instance != null ? BeatClock.Instance.SecondsPerBeat : 0.5f;
            yield return new WaitForSeconds(duration);

            foreach (var tower in allTowers)
            {
                if (tower != null)
                {
                    tower.SetRhythmMultiplier(1.0f);
                }
            }

            rhythmBoostCoroutine = null;
        }

        private void AddUltimateCharge(float baseAmount)
        {
            // Bonus adicional ligero según el combo acumulado
            float comboBonus = Mathf.Min(0.04f, currentCombo * 0.002f);
            float totalCharge = baseAmount + comboBonus;
            OnUltimateChargeGenerated?.Invoke(totalCharge);
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null) return;

            if (feedbackFadeCoroutine != null)
            {
                StopCoroutine(feedbackFadeCoroutine);
            }

            feedbackFadeCoroutine = StartCoroutine(FeedbackFadeRoutine(message, color));
        }

        private IEnumerator FeedbackFadeRoutine(string message, Color color)
        {
            feedbackText.text = message;
            feedbackText.color = color;
            feedbackText.transform.localScale = Vector3.one * 1.3f;

            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                feedbackText.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, t);
                yield return null;
            }

            feedbackText.transform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.2f);
            feedbackText.text = "";
        }

        private void UpdateComboUI()
        {
            if (comboText == null) return;

            if (currentCombo > 1)
            {
                comboText.text = $"<size=70%>COMBO</size>\n<color=#00FFFF><b>{currentCombo}</b></color>";
            }
            else
            {
                comboText.text = "";
            }
        }

        /// <summary>
        /// Anima el anillo visual o metrónomo pulsando al ritmo del compás musical.
        /// </summary>
        private void AnimatePulseRing(float progress)
        {
            if (pulseRing == null) return;

            // Escala de contracción hacia el pulso central (1.0 -> 0.0)
            float scale = Mathf.Lerp(1.25f, 1.0f, progress);
            pulseRing.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
