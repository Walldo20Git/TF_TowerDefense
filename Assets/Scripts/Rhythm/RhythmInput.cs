using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Core;
using ConcertDefense.Towers;

namespace ConcertDefense.Rhythm
{
    /// <summary>
    /// Botón Beat (GDD 4.4):
    /// - Evalúa cada toque contra el BeatClock (Perfect, Good, Miss).
    /// - Perfect aplica ×1.5 de daño a todas las torres durante ese compás.
    /// - Los aciertos seguidos forman un combo que carga el Ultimate.
    /// </summary>
    public class RhythmInput : MonoBehaviour
    {
        public static RhythmInput Instance { get; private set; }

        [Header("Referencias de UI")]
        [Tooltip("Botón Beat (independiente de los toques sobre el campo).")]
        [SerializeField] private Button beatButton;

        [Tooltip("Texto con la calificación del toque (PERFECT / GOOD / MISS).")]
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Tooltip("Texto con el combo actual.")]
        [SerializeField] private TextMeshProUGUI comboText;

        [Tooltip("Anillo que se cierra sobre el botón al llegar cada tiempo.")]
        [SerializeField] private RectTransform pulseRing;

        [Header("Carga de Ultimate por acierto")]
        [SerializeField] private float perfectChargeAmount = 0.06f;
        [SerializeField] private float goodChargeAmount = 0.03f;

        [Header("Paleta")]
        [SerializeField] private Color perfectColor = new Color(0f, 1f, 0.9f);
        [SerializeField] private Color goodColor = new Color(1f, 0.2f, 0.7f);
        [SerializeField] private Color missColor = new Color(0.7f, 0.7f, 0.75f);

        private int currentCombo;
        private int maxCombo;
        private int lastScoredBeat = -1;
        private float boostTimer;
        private Coroutine feedbackRoutine;
        private PointerDownRelay relay;

        public int CurrentCombo => currentCombo;
        public int MaxCombo => maxCombo;

        public static event Action<HitAccuracy, int> OnRhythmHit;
        public static event Action<float> OnUltimateChargeGenerated;

        private void Awake()
        {
            Instance = this;
            Tower.RhythmMultiplier = 1f;
        }

        private void Start()
        {
            if (beatButton != null)
            {
                relay = beatButton.gameObject.GetComponent<PointerDownRelay>();
                if (relay == null) relay = beatButton.gameObject.AddComponent<PointerDownRelay>();
                relay.OnDown += OnBeatPressed;
            }

            BeatClock.OnBeatPulse += AnimatePulseRing;

            if (feedbackText != null) feedbackText.text = "";
            UpdateComboUI();
        }

        private void OnDestroy()
        {
            if (relay != null) relay.OnDown -= OnBeatPressed;
            BeatClock.OnBeatPulse -= AnimatePulseRing;
            Tower.RhythmMultiplier = 1f;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Atajo de teclado para probar en el editor
            if (PointerInput.BeatKeyPressed) OnBeatPressed();

            if (boostTimer > 0f)
            {
                boostTimer -= Time.deltaTime;
                if (boostTimer <= 0f) Tower.RhythmMultiplier = 1f;
            }
        }

        /// <summary>
        /// Se ejecuta al apoyar el dedo sobre el botón Beat.
        /// </summary>
        public void OnBeatPressed()
        {
            if (!BeatClock.IsRunning)
            {
                GameMessages.Show("El ritmo empieza cuando coloques el escenario.");
                return;
            }

            HitAccuracy accuracy = BeatClock.Instance.EvaluateTap(out _, out int beat);

            // Un mismo tiempo solo puntúa una vez: machacar el botón no da combo
            if (accuracy != HitAccuracy.Miss && beat == lastScoredBeat) return;

            switch (accuracy)
            {
                case HitAccuracy.Perfect:
                    lastScoredBeat = beat;
                    currentCombo++;
                    Tower.RhythmMultiplier = 1.5f;
                    boostTimer = BeatClock.Instance.SecondsPerBeat;
                    AddUltimateCharge(perfectChargeAmount);
                    ShowFeedback("PERFECT!", perfectColor);
                    Sfx.Play(SfxId.Perfect);
                    break;

                case HitAccuracy.Good:
                    lastScoredBeat = beat;
                    currentCombo++;
                    AddUltimateCharge(goodChargeAmount);
                    ShowFeedback("GOOD", goodColor);
                    Sfx.Play(SfxId.Good);
                    break;

                default:
                    currentCombo = 0;
                    ShowFeedback("MISS", missColor);
                    break;
            }

            maxCombo = Mathf.Max(maxCombo, currentCombo);
            UpdateComboUI();
            OnRhythmHit?.Invoke(accuracy, currentCombo);
        }

        private void AddUltimateCharge(float baseAmount)
        {
            // El combo acumulado da un pequeño extra
            float comboBonus = Mathf.Min(0.04f, currentCombo * 0.002f);
            OnUltimateChargeGenerated?.Invoke(baseAmount + comboBonus);
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null) return;

            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(FeedbackRoutine(message, color));
        }

        private IEnumerator FeedbackRoutine(string message, Color color)
        {
            feedbackText.text = message;
            feedbackText.color = color;

            float elapsed = 0f;
            const float duration = 0.4f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                feedbackText.transform.localScale = Vector3.Lerp(Vector3.one * 1.35f, Vector3.one, elapsed / duration);
                yield return null;
            }

            feedbackText.transform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.25f);
            feedbackText.text = "";
            feedbackRoutine = null;
        }

        private void UpdateComboUI()
        {
            if (comboText == null) return;
            comboText.text = currentCombo > 1 ? $"COMBO x{currentCombo}" : "";
        }

        private void AnimatePulseRing(float progress)
        {
            if (pulseRing == null) return;

            // El anillo se cierra sobre el botón justo al llegar el tiempo
            float scale = Mathf.Lerp(1.45f, 1f, progress);
            pulseRing.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
