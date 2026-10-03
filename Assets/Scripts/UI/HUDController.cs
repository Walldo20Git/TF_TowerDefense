using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.AR;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.UI
{
    /// <summary>
    /// HUD en Screen Space Overlay (GDD 7). Todo el HUD de juego está visible desde que arranca la partida:
    /// monedas, Ánimo, oleada, botón Iniciar oleada, Beat y Ultimate. Encima se muestran, según el momento,
    /// la guía de escaneo AR, la barra del jefe, los avisos y los paneles de Game Over / Victoria.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("Guía de Escaneo y Colocación AR")]
        [SerializeField] private GameObject arGuidancePanel;
        [SerializeField] private TextMeshProUGUI arGuidanceText;

        [Header("HUD de Juego")]
        [Tooltip("Contenedor del HUD de juego. Permanece activo desde el inicio.")]
        [SerializeField] private GameObject gameplayHudRoot;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI stageHealthText;
        [Tooltip("Relleno de la barra de Ánimo.")]
        [SerializeField] private RectTransform stageHealthFill;
        [SerializeField] private TextMeshProUGUI waveText;

        [Header("Control de Oleadas")]
        [SerializeField] private Button startWaveButton;
        [SerializeField] private TextMeshProUGUI startWaveLabel;
        [Tooltip("Botón para volver a colocar el campo (solo entre oleadas).")]
        [SerializeField] private Button relocateButton;

        [Header("Barra de Jefe")]
        [SerializeField] private GameObject bossBarContainer;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private RectTransform bossHealthFill;

        [Header("Avisos")]
        [SerializeField] private GameObject toastPanel;
        [SerializeField] private TextMeshProUGUI toastText;

        [Header("Fin de Partida")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button restartGameOverButton;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Button restartVictoryButton;

        private BossBase activeBoss;
        private float toastTimer;

        private void Awake()
        {
            Instance = this;

            // El HUD de juego se ve desde el primer fotograma; solo se ocultan los paneles eventuales
            if (gameplayHudRoot != null) gameplayHudRoot.SetActive(true);
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
            if (toastPanel != null) toastPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
        }

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnCoinsChanged += HandleCoinsChanged;
                gm.OnHealthChanged += HandleHealthChanged;
                gm.OnWaveChanged += HandleWaveChanged;
                gm.OnGameStateChanged += HandleGameStateChanged;
            }

            WaveSpawner ws = WaveSpawner.Instance;
            if (ws != null)
            {
                ws.OnWaveStarted += HandleWaveStarted;
                ws.OnWaveCompleted += HandleWaveCompleted;
                ws.OnRemainingEnemiesChanged += HandleRemainingEnemiesChanged;
            }

            BossBase.OnBossSpawned += HandleBossSpawned;
            BossBase.OnBossDefeated += HandleBossDefeated;
            GameMessages.OnMessage += ShowToast;

            if (startWaveButton != null) startWaveButton.onClick.AddListener(OnStartWaveClicked);
            if (relocateButton != null) relocateButton.onClick.AddListener(OnRelocateClicked);
            if (restartGameOverButton != null) restartGameOverButton.onClick.AddListener(OnRestartClicked);
            if (restartVictoryButton != null) restartVictoryButton.onClick.AddListener(OnRestartClicked);

            // Pintar el estado inicial
            if (gm != null)
            {
                HandleCoinsChanged(gm.CurrentCoins);
                HandleHealthChanged(gm.CurrentHealth, gm.MaxHealth);
                HandleWaveChanged(gm.CurrentWave, gm.TotalWaves);
                HandleGameStateChanged(gm.CurrentState);
            }
            RefreshWaveControls();
        }

        private void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnCoinsChanged -= HandleCoinsChanged;
                gm.OnHealthChanged -= HandleHealthChanged;
                gm.OnWaveChanged -= HandleWaveChanged;
                gm.OnGameStateChanged -= HandleGameStateChanged;
            }

            WaveSpawner ws = WaveSpawner.Instance;
            if (ws != null)
            {
                ws.OnWaveStarted -= HandleWaveStarted;
                ws.OnWaveCompleted -= HandleWaveCompleted;
                ws.OnRemainingEnemiesChanged -= HandleRemainingEnemiesChanged;
            }

            BossBase.OnBossSpawned -= HandleBossSpawned;
            BossBase.OnBossDefeated -= HandleBossDefeated;
            GameMessages.OnMessage -= ShowToast;

            if (activeBoss != null) activeBoss.OnHealthChanged -= HandleBossHealthChanged;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f && toastPanel != null) toastPanel.SetActive(false);
            }

            // Atajo de teclado para probar en el editor
            if (PointerInput.EnterPressed) OnStartWaveClicked();
        }

        // ---------- Estado del juego ----------

        private void HandleGameStateChanged(GameState state)
        {
            bool fallback = ARPlacementController.Instance != null && ARPlacementController.Instance.IsFallbackMode;

            switch (state)
            {
                case GameState.Scanning:
                    SetGuidance(Application.isEditor
                        ? "Editor: apunta la cámara simulada a una superficie, o pulsa INICIAR OLEADA (o Espacio) para colocar el escenario."
                        : "Mueve el teléfono despacio apuntando a una mesa o al piso para detectar la superficie.");
                    break;

                case GameState.Placing:
                    SetGuidance(fallback
                        ? "Modo de prueba sin AR: haz clic o pulsa INICIAR OLEADA para colocar el escenario."
                        : "¡Superficie detectada! Toca la pantalla para colocar el escenario.");
                    break;

                case GameState.Playing:
                    if (arGuidancePanel != null) arGuidancePanel.SetActive(false);
                    break;

                case GameState.GameOver:
                    HideToast();
                    if (arGuidancePanel != null) arGuidancePanel.SetActive(false);
                    if (bossBarContainer != null) bossBarContainer.SetActive(false);
                    if (gameOverPanel != null) gameOverPanel.SetActive(true);
                    break;

                case GameState.Victory:
                    HideToast();
                    if (arGuidancePanel != null) arGuidancePanel.SetActive(false);
                    if (bossBarContainer != null) bossBarContainer.SetActive(false);
                    if (victoryPanel != null) victoryPanel.SetActive(true);
                    break;
            }

            RefreshWaveControls();
        }

        private void SetGuidance(string text)
        {
            if (arGuidancePanel != null) arGuidancePanel.SetActive(true);
            if (arGuidanceText != null) arGuidanceText.text = text;
        }

        // ---------- Recursos ----------

        private void HandleCoinsChanged(int coins)
        {
            if (coinsText != null) coinsText.text = $"MONEDAS  <b>{coins}</b>";
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (stageHealthText != null) stageHealthText.text = $"ÁNIMO  {current} / {max}";
            SetFill(stageHealthFill, max > 0 ? (float)current / max : 0f);
        }

        private void HandleWaveChanged(int current, int total)
        {
            if (waveText != null) waveText.text = current == 0 ? $"OLEADA  - / {total}" : $"OLEADA  {current} / {total}";
        }

        // ---------- Oleadas ----------

        private void HandleWaveStarted(int waveNumber)
        {
            RefreshWaveControls();
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            RefreshWaveControls();

            WaveSpawner ws = WaveSpawner.Instance;
            if (ws != null && ws.CurrentWaveIndex < ws.TotalWaves)
            {
                ShowToast($"¡Oleada {waveNumber} superada! Construye o mejora torres antes de la siguiente.", 4f);
            }
        }

        private void HandleRemainingEnemiesChanged(int remaining)
        {
            RefreshWaveControls();
        }

        /// <summary>
        /// El botón Iniciar oleada siempre está en pantalla; durante el combate muestra los glitches restantes.
        /// </summary>
        private void RefreshWaveControls()
        {
            WaveSpawner ws = WaveSpawner.Instance;
            GameManager gm = GameManager.Instance;
            bool waveActive = ws != null && ws.IsWaveInProgress;
            bool finished = gm != null && (gm.CurrentState == GameState.GameOver || gm.CurrentState == GameState.Victory);

            if (startWaveButton != null) startWaveButton.interactable = !waveActive && !finished;

            if (startWaveLabel != null)
            {
                if (finished) startWaveLabel.text = "FIN DE LA PARTIDA";
                else if (waveActive) startWaveLabel.text = $"GLITCHES: {ws.ActiveEnemies}";
                else if (gm != null && !gm.IsPlaying && !finished) startWaveLabel.text = "INICIAR OLEADA";
                else startWaveLabel.text = ws != null ? $"INICIAR OLEADA {Mathf.Min(ws.CurrentWaveIndex + 1, ws.TotalWaves)}" : "INICIAR OLEADA";
            }

            if (relocateButton != null)
            {
                relocateButton.interactable = !waveActive && gm != null && gm.IsPlaying;
            }
        }

        private void OnStartWaveClicked()
        {
            GameManager gm = GameManager.Instance;
            WaveSpawner ws = WaveSpawner.Instance;
            if (gm == null || ws == null) return;
            if (startWaveButton != null && !startWaveButton.interactable) return;

            // Si el campo aún no está colocado, el botón lo coloca (si hay un punto válido)
            if (!gm.IsPlaying)
            {
                ARPlacementController placer = ARPlacementController.Instance;
                if (placer == null || !placer.PlaceBattlefield())
                {
                    ShowToast("Todavía no se detecta una superficie. Mueve el teléfono despacio sobre la mesa o el piso.", 3f);
                }
                return;
            }

            if (!ws.StartNextWave())
            {
                ShowToast("No se puede iniciar la oleada todavía.", 2f);
            }
        }

        private void OnRelocateClicked()
        {
            if (ARPlacementController.Instance != null) ARPlacementController.Instance.ResetPlacement();
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        }

        // ---------- Jefe ----------

        private void HandleBossSpawned(BossBase boss)
        {
            if (activeBoss != null) activeBoss.OnHealthChanged -= HandleBossHealthChanged;

            activeBoss = boss;
            activeBoss.OnHealthChanged += HandleBossHealthChanged;

            if (bossBarContainer != null) bossBarContainer.SetActive(true);
            if (bossNameText != null) bossNameText.text = $"JEFE: {boss.BossTitle}";
            HandleBossHealthChanged(boss.CurrentHealth, boss.MaxHealth);
        }

        private void HandleBossHealthChanged(float current, float max)
        {
            SetFill(bossHealthFill, max > 0f ? current / max : 0f);
        }

        private void HandleBossDefeated(BossBase boss)
        {
            if (activeBoss != boss) return;

            activeBoss.OnHealthChanged -= HandleBossHealthChanged;
            activeBoss = null;
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
        }

        // ---------- Avisos ----------

        private void HideToast()
        {
            toastTimer = 0f;
            if (toastPanel != null) toastPanel.SetActive(false);
        }

        public void ShowToast(string text, float seconds)
        {
            if (toastPanel == null || toastText == null) return;

            // Con la partida terminada ya no se muestran avisos sobre el panel final
            GameManager gm = GameManager.Instance;
            if (gm != null && (gm.CurrentState == GameState.GameOver || gm.CurrentState == GameState.Victory)) return;

            toastText.text = text;
            toastPanel.SetActive(true);
            toastTimer = seconds;
        }

        private static void SetFill(RectTransform fill, float pct)
        {
            if (fill == null) return;

            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}
