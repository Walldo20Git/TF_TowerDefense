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
    /// Reloj musical maestro sincronizado mediante AudioSettings.dspTime (GDD 4.4):
    /// - Mantiene el compás exacto (120 BPM por defecto) sin desincronización por fotogramas.
    /// - Emite eventos en cada pulso de compás (OnBeat) para sincronizar disparos y animaciones.
    /// - Evalúa la precisión rítmica del botón Beat (Perfect, Good o Miss).
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        public static BeatClock Instance { get; private set; }

        [Header("Configuración de Ritmo (GDD 4.4)")]
        [Tooltip("Pulsos por minuto de la pista musical (ej: 120 BPM).")]
        [SerializeField] private float bpm = 120f;

        [Tooltip("Ventana de tolerancia para acierto PERFECT en segundos (±0.08 s).")]
        [SerializeField] private float perfectWindow = 0.08f;

        [Tooltip("Ventana de tolerancia para acierto GOOD en segundos (±0.15 s).")]
        [SerializeField] private float goodWindow = 0.15f;

        [Header("Pista Musical")]
        [Tooltip("Fuente de audio con la pista de combate.")]
        [SerializeField] private AudioSource musicSource;

        [Tooltip("Si es true, la música arranca automáticamente cuando GameManager pasa a estado Playing.")]
        [SerializeField] private bool autoStartOnPlaying = true;

        // Variables de sincronización DSP
        private double dspStartTime;
        private double secondsPerBeat;
        private int lastBeatCount = -1;
        private bool isRunning = false;

        public float BPM => bpm;
        public float SecondsPerBeat => (float)secondsPerBeat;
        public bool IsRunning => isRunning;
        public int CurrentBeatCount { get; private set; } = 0;

        // Eventos
        public event Action<int> OnBeat;              // Emitido en cada pulso (1, 2, 3, 4...)
        public event Action<float> OnBeatPulse;       // Progreso normalizado de 0 a 1 para animaciones de UI

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CalculateBeatTiming();
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Start()
        {
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing && autoStartOnPlaying)
            {
                StartClock();
            }
        }

        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.Playing && autoStartOnPlaying)
            {
                StartClock();
            }
            else if (newState == GameState.GameOver || newState == GameState.Victory)
            {
                StopClock();
            }
        }

        /// <summary>
        /// Calcula la duración exacta de cada pulso de compás.
        /// </summary>
        public void CalculateBeatTiming()
        {
            bpm = Mathf.Max(30f, bpm);
            secondsPerBeat = 60.0 / bpm;
        }

        /// <summary>
        /// Inicia la reproducción musical y la medición temporal en DSP time.
        /// </summary>
        public void StartClock()
        {
            CalculateBeatTiming();
            dspStartTime = AudioSettings.dspTime + 0.1; // Pequeño buffer para sincronización perfecta de hardware

            if (musicSource != null && musicSource.clip != null)
            {
                musicSource.PlayScheduled(dspStartTime);
            }

            lastBeatCount = -1;
            isRunning = true;
            Debug.Log($"[BeatClock] Reloj rítmico iniciado a {bpm} BPM (1 pulso cada {secondsPerBeat:F3}s).");
        }

        /// <summary>
        /// Detiene la música y el reloj.
        /// </summary>
        public void StopClock()
        {
            isRunning = false;
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        private void Update()
        {
            if (!isRunning) return;

            double songTime = AudioSettings.dspTime - dspStartTime;
            if (songTime < 0) return; // Esperando el dspStartTime programado

            // Número de pulsos transcurridos
            double currentBeatFloat = songTime / secondsPerBeat;
            int currentBeat = (int)currentBeatFloat;

            // Detectar transición de un nuevo compás
            if (currentBeat > lastBeatCount)
            {
                lastBeatCount = currentBeat;
                CurrentBeatCount = currentBeat;
                OnBeat?.Invoke(CurrentBeatCount);
            }

            // Progreso del pulso actual (0.0 a 1.0) para animar el botón de la UI
            float pulseProgress = (float)(currentBeatFloat - currentBeat);
            OnBeatPulse?.Invoke(pulseProgress);
        }

        /// <summary>
        /// Evalúa la precisión del toque del jugador en relación al pulso de compás más cercano.
        /// </summary>
        public HitAccuracy EvaluateTap(out float timingOffset)
        {
            if (!isRunning)
            {
                timingOffset = 0f;
                return HitAccuracy.Miss;
            }

            double songTime = AudioSettings.dspTime - dspStartTime;
            double currentBeatFloat = songTime / secondsPerBeat;
            double nearestBeat = Math.Round(currentBeatFloat);

            // Diferencia en segundos entre el toque y el pulso más próximo
            double offsetInSeconds = (currentBeatFloat - nearestBeat) * secondsPerBeat;
            timingOffset = (float)offsetInSeconds;

            float absOffset = Mathf.Abs(timingOffset);

            if (absOffset <= perfectWindow)
            {
                return HitAccuracy.Perfect;
            }
            else if (absOffset <= goodWindow)
            {
                return HitAccuracy.Good;
            }
            else
            {
                return HitAccuracy.Miss;
            }
        }
    }
}
