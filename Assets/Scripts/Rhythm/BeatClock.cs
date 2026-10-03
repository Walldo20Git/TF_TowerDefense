using System;
using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Rhythm
{
    public enum HitAccuracy
    {
        Miss,    // Fuera de tiempo
        Good,    // Acierto aceptable (≈ ±0.15 s)
        Perfect  // Acierto perfecto (≈ ±0.08 s)
    }

    /// <summary>
    /// Reloj musical maestro (GDD 4.4):
    /// - Lleva un compás fijo (120 BPM) sincronizado con la pista mediante AudioSettings.dspTime.
    /// - Emite un evento en cada tiempo y en cada medio tiempo; las torres disparan con ellos.
    /// - Evalúa la precisión del botón Beat (Perfect, Good o Miss).
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        public static BeatClock Instance { get; private set; }

        /// <summary>Para pruebas automáticas: usar el tiempo de juego en lugar del reloj de audio.</summary>
        public static bool ForceGameTime;

        [Header("Configuración de Ritmo (GDD 4.4)")]
        [Tooltip("Pulsos por minuto de la pista musical.")]
        [SerializeField] private float bpm = 120f;

        [Tooltip("Ventana de acierto PERFECT en segundos (±).")]
        [SerializeField] private float perfectWindow = 0.08f;

        [Tooltip("Ventana de acierto GOOD en segundos (±).")]
        [SerializeField] private float goodWindow = 0.15f;

        [Header("Pista Musical")]
        [Tooltip("Fuente de audio con la pista de combate (BPM fijo).")]
        [SerializeField] private AudioSource musicSource;

        [Tooltip("Arranca solo cuando el campo queda colocado (estado Playing).")]
        [SerializeField] private bool autoStartOnPlaying = true;

        private double startTime;
        private double secondsPerBeat = 0.5;
        private double dspAtAwake;
        private int lastHalfBeat = -1;
        private bool isRunning;
        private bool useAudioClock;

        public float BPM => bpm;
        public float SecondsPerBeat => (float)secondsPerBeat;
        public int CurrentBeat { get; private set; }

        public static bool IsRunning => Instance != null && Instance.isRunning;

        /// <summary>Cada tiempo del compás (0, 1, 2...).</summary>
        public static event Action<int> OnBeat;
        /// <summary>Cada medio tiempo; los pares coinciden con OnBeat.</summary>
        public static event Action<int> OnHalfBeat;
        /// <summary>Progreso 0–1 dentro del tiempo actual, para animar la UI.</summary>
        public static event Action<float> OnBeatPulse;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (musicSource == null) musicSource = GetComponent<AudioSource>();
            dspAtAwake = AudioSettings.dspTime;
            secondsPerBeat = 60.0 / Mathf.Max(30f, bpm);
        }

        private void Start()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            if (GameManager.Instance.IsPlaying && autoStartOnPlaying) StartClock();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            if (Instance == this) Instance = null;
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Playing && autoStartOnPlaying)
            {
                if (!isRunning) StartClock();
            }
            else if (state == GameState.GameOver || state == GameState.Victory)
            {
                StopClock();
            }
        }

        private double Now => useAudioClock ? AudioSettings.dspTime : Time.timeAsDouble;

        /// <summary>
        /// Arranca la música y el compás.
        /// </summary>
        public void StartClock()
        {
            secondsPerBeat = 60.0 / Mathf.Max(30f, bpm);

            // Si el dispositivo de audio no avanza (sin salida de sonido), se usa el tiempo de juego
            useAudioClock = !ForceGameTime && AudioSettings.dspTime > dspAtAwake;
            startTime = Now + 0.1;

            if (musicSource != null && musicSource.clip != null)
            {
                musicSource.loop = true;
                if (useAudioClock) musicSource.PlayScheduled(startTime);
                else musicSource.PlayDelayed(0.1f);
            }

            lastHalfBeat = -1;
            isRunning = true;
        }

        /// <summary>
        /// Detiene la música y el compás.
        /// </summary>
        public void StopClock()
        {
            isRunning = false;
            if (musicSource != null) musicSource.Stop();
        }

        private void Update()
        {
            if (!isRunning) return;

            double songTime = Now - startTime;
            if (songTime < 0) return;

            double halfBeatFloat = songTime / (secondsPerBeat * 0.5);
            int halfBeat = (int)halfBeatFloat;

            if (halfBeat > lastHalfBeat)
            {
                lastHalfBeat = halfBeat;
                OnHalfBeat?.Invoke(halfBeat);

                if (halfBeat % 2 == 0)
                {
                    CurrentBeat = halfBeat / 2;
                    OnBeat?.Invoke(CurrentBeat);
                }
            }

            double beatFloat = songTime / secondsPerBeat;
            OnBeatPulse?.Invoke((float)(beatFloat - Math.Floor(beatFloat)));
        }

        /// <summary>
        /// Evalúa un toque respecto al tiempo del compás más cercano.
        /// </summary>
        public HitAccuracy EvaluateTap(out float timingOffset, out int nearestBeat)
        {
            timingOffset = 0f;
            nearestBeat = -1;
            if (!isRunning) return HitAccuracy.Miss;

            double beatFloat = (Now - startTime) / secondsPerBeat;
            double nearest = Math.Round(beatFloat);
            nearestBeat = (int)nearest;
            timingOffset = (float)((beatFloat - nearest) * secondsPerBeat);

            float absOffset = Mathf.Abs(timingOffset);
            if (absOffset <= perfectWindow) return HitAccuracy.Perfect;
            if (absOffset <= goodWindow) return HitAccuracy.Good;
            return HitAccuracy.Miss;
        }
    }
}
