using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.UI
{
    /// <summary>
    /// Controlador maestro de la interfaz de usuario en Screen Space Overlay (GDD 7):
    /// - Muestra y sincroniza monedas, Ánimo del escenario y progreso de oleadas.
    /// - Controla el botón de Iniciar Oleada (oculto en combate, visible en descansos).
    /// - Despliega la barra de vida superior de Jefes (Boss Health Bar) al aparecer uno.
    /// - Muestra los paneles de Game Over y Victoria con botones de reinicio.
    /// - Guía al usuario en las fases de escaneo y colocación AR.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("Guía de Escaneo y Colocación AR")]
        [Tooltip("Panel con instrucciones de escaneo de superficies (visible en Scanning y Placing).")]
        [SerializeField] private GameObject arGuidancePanel;
        [SerializeField] private TextMeshProUGUI arGuidanceText;

        [Header("HUD de Combate (Recursos)")]
        [Tooltip("Contenedor general del HUD durante la partida activa.")]
        [SerializeField] private GameObject gameplayHudRoot;

        [Tooltip("Texto para mostrar las monedas actuales.")]
        [SerializeField] private TextMeshProUGUI coinsText;

        [Tooltip("Texto que muestra la vida del escenario ('Ánimo').")]
        [SerializeField] private TextMeshProUGUI stageHealthText;

        [Tooltip("Barra de vida visual del escenario.")]
        [SerializeField] private Slider stageHealthSlider;

        [Tooltip("Texto indicador de oleada (ej: 'Oleada 1 / 4').")]
        [SerializeField] private TextMeshProUGUI waveText;

        [Header("Control de Oleadas")]
        [Tooltip("Botón grande para lanzar la siguiente oleada de glitches.")]
        [SerializeField] private Button startWaveButton;

        [Header("Barra de Jefe (Pantalla Superior)")]
        [Tooltip("Contenedor superior de la barra de vida del jefe.")]
        [SerializeField] private GameObject bossBarContainer;

        [Tooltip("Texto con el nombre y título del jefe en combate.")]
        [SerializeField] private TextMeshProUGUI bossNameText;

        [Tooltip("Slider que representa la vida actual del jefe.")]
        [SerializeField] private Slider bossHealthSlider;

        [Header("Paneles de Fin de Partida")]
        [Tooltip("Panel desplegado al llegar el Ánimo a 0.")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button restartGameOverButton;

        [Tooltip("Panel desplegado al superar las 4 oleadas.")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Button restartVictoryButton;

        // Referencia al jefe actual para desuscribirse de eventos
        private BossBase activeBoss;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CleanUpDuplicatesAndEnsureReferences();

            // Ocultar inmediatamente todo el HUD de combate y paneles de fin de partida
            if (gameplayHudRoot != null) gameplayHudRoot.SetActive(false);
            if (bossBarContainer != null) bossBarContainer.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
        }

        /// <summary>
        /// Limpia elementos huérfanos o duplicados en el Canvas y asegura que las referencias estén vinculadas.
        /// </summary>
        private void CleanUpDuplicatesAndEnsureReferences()
        {
            // Identificar los paneles principales legítimos
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "AR_GuidancePanel")
                {
                    if (arGuidancePanel == null) arGuidancePanel = child.gameObject;
                    else if (child.gameObject != arGuidancePanel) Destroy(child.gameObject);
                }
                else if (child.name == "GameplayHUD_Root")
                {
                    if (gameplayHudRoot == null) gameplayHudRoot = child.gameObject;
                    else if (child.gameObject != gameplayHudRoot) Destroy(child.gameObject);
                }
                else if (child.name == "GameOverPanel")
                {
                    if (gameOverPanel == null) gameOverPanel = child.gameObject;
                    else if (child.gameObject != gameOverPanel) Destroy(child.gameObject);
                }
                else if (child.name == "VictoryPanel")
                {
                    if (victoryPanel == null) victoryPanel = child.gameObject;
                    else if (child.gameObject != victoryPanel) Destroy(child.gameObject);
                }
            }

            // Asegurar que el GameplayHUD_Root no tenga una imagen invisible bloqueando raycasts
            if (gameplayHudRoot != null)
            {
                Image rootImg = gameplayHudRoot.GetComponent<Image>();
                if (rootImg != null) rootImg.raycastTarget = false;

                if (startWaveButton == null)
                {
                    var btnTransform = gameplayHudRoot.transform.Find("StartWaveButton");
                    if (btnTransform != null) startWaveButton = btnTransform.GetComponent<Button>();
                    if (startWaveButton == null) startWaveButton = gameplayHudRoot.GetComponentInChildren<Button>(true);
                }

                if (coinsText == null)
                {
                    var t = gameplayHudRoot.transform.Find("TopBar/CoinsText");
                    if (t != null) coinsText = t.GetComponent<TextMeshProUGUI>();
                }
                if (stageHealthText == null)
                {
                    var t = gameplayHudRoot.transform.Find("TopBar/StageHealthSlider/HealthText");
                    if (t != null) stageHealthText = t.GetComponent<TextMeshProUGUI>();
                }
                if (stageHealthSlider == null)
                {
                    var t = gameplayHudRoot.transform.Find("TopBar/StageHealthSlider");
                    if (t != null) stageHealthSlider = t.GetComponent<Slider>();
                }
                if (waveText == null)
                {
                    var t = gameplayHudRoot.transform.Find("TopBar/WaveText");
                    if (t != null) waveText = t.GetComponent<TextMeshProUGUI>();
                }

                // Asegurar que TopBar y WaveText se adapten responsivemente y el texto nunca quede cortado
                var topBarTransform = gameplayHudRoot.transform.Find("TopBar");
                if (topBarTransform != null)
                {
                    RectTransform tbRT = topBarTransform.GetComponent<RectTransform>();
                    if (tbRT != null)
                    {
                        tbRT.anchorMin = new Vector2(0f, 1f);
                        tbRT.anchorMax = new Vector2(1f, 1f);
                        tbRT.pivot = new Vector2(0.5f, 1f);
                        tbRT.anchoredPosition = new Vector2(0f, -10f);
                        tbRT.sizeDelta = new Vector2(-40f, 75f);
                    }
                }

                if (waveText != null)
                {
                    RectTransform wtRT = waveText.rectTransform;
                    wtRT.anchorMin = new Vector2(1f, 0.5f);
                    wtRT.anchorMax = new Vector2(1f, 0.5f);
                    wtRT.pivot = new Vector2(1f, 0.5f);
                    wtRT.anchoredPosition = new Vector2(-25f, 0f);
                    wtRT.sizeDelta = new Vector2(220f, 50f);
                    waveText.alignment = TextAlignmentOptions.MidlineRight;
                    waveText.raycastTarget = false;
                }
            }

            // Asegurar que las etiquetas de texto de botones no bloqueen los raycasts del botón
            if (startWaveButton != null)
            {
                foreach (var tmp in startWaveButton.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    tmp.raycastTarget = false;
                }
            }
        }

        private void Update()
        {
            #if UNITY_EDITOR || UNITY_STANDALONE
            // Atajo de teclado en PC/Editor: Iniciar Oleada con Enter o P
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.P))
            {
                if (startWaveButton != null && startWaveButton.gameObject.activeInHierarchy)
                {
                    Debug.Log("[HUDController] Atajo de teclado activado (Enter/P) para Iniciar Oleada.");
                    OnStartWaveClicked();
                }
            }
            #endif
        }

        private bool isSubscribed = false;

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            // Re-ejecutar comprobación para auto-vincular cualquier elemento rezagado
            CleanUpDuplicatesAndEnsureReferences();

            // Asegurar suscripción en Start por si GameManager o WaveSpawner inicializaron después
            SubscribeToEvents();

            // Ocultar paneles de fin de partida y barra de jefe por defecto
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (bossBarContainer != null) bossBarContainer.SetActive(false);

            // Si el escenario ya está presente en la escena, asegurar inmediatamente el estado Playing
            if (GameObject.Find("Battlefield") != null || GameObject.Find("Battlefield(Clone)") != null)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                {
                    GameManager.Instance.ChangeState(GameState.Playing);
                }
            }

            // Refrescar datos con el estado actual de GameManager
            if (GameManager.Instance != null)
            {
                HandleCoinsChanged(GameManager.Instance.CurrentCoins);
                HandleHealthChanged(GameManager.Instance.CurrentHealth, GameManager.Instance.MaxHealth);
                HandleWaveChanged(GameManager.Instance.CurrentWave, GameManager.Instance.TotalWaves);
                HandleGameStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void SubscribeToEvents()
        {
            if (isSubscribed) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                GameManager.Instance.OnHealthChanged += HandleHealthChanged;
                GameManager.Instance.OnWaveChanged += HandleWaveChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }

            if (WaveSpawner.Instance != null)
            {
                WaveSpawner.Instance.OnWaveStarted += HandleWaveStarted;
                WaveSpawner.Instance.OnWaveCompleted += HandleWaveCompleted;
            }

            BossBase.OnBossSpawned += HandleBossSpawned;
            BossBase.OnBossDefeated += HandleBossDefeated;

            if (startWaveButton != null)
            {
                startWaveButton.onClick.RemoveListener(OnStartWaveClicked);
                startWaveButton.onClick.AddListener(OnStartWaveClicked);
            }

            if (restartGameOverButton != null)
            {
                restartGameOverButton.onClick.RemoveListener(OnRestartClicked);
                restartGameOverButton.onClick.AddListener(OnRestartClicked);
            }

            if (restartVictoryButton != null)
            {
                restartVictoryButton.onClick.RemoveListener(OnRestartClicked);
                restartVictoryButton.onClick.AddListener(OnRestartClicked);
            }

            isSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!isSubscribed) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                GameManager.Instance.OnHealthChanged -= HandleHealthChanged;
                GameManager.Instance.OnWaveChanged -= HandleWaveChanged;
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            if (WaveSpawner.Instance != null)
            {
                WaveSpawner.Instance.OnWaveStarted -= HandleWaveStarted;
                WaveSpawner.Instance.OnWaveCompleted -= HandleWaveCompleted;
            }

            BossBase.OnBossSpawned -= HandleBossSpawned;
            BossBase.OnBossDefeated -= HandleBossDefeated;

            if (startWaveButton != null) startWaveButton.onClick.RemoveListener(OnStartWaveClicked);
            if (restartGameOverButton != null) restartGameOverButton.onClick.RemoveListener(OnRestartClicked);
            if (restartVictoryButton != null) restartVictoryButton.onClick.RemoveListener(OnRestartClicked);

            if (activeBoss != null)
            {
                activeBoss.OnHealthChanged -= HandleBossHealthChanged;
            }

            isSubscribed = false;
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Scanning:
                    if (arGuidancePanel != null) arGuidancePanel.SetActive(true);
                    if (arGuidanceText != null) arGuidanceText.text = "Apunta tu cámara a una mesa o piso para detectar planos...";
                    if (gameplayHudRoot != null) gameplayHudRoot.SetActive(false);
                    break;

                case GameState.Placing:
                    if (arGuidancePanel != null) arGuidancePanel.SetActive(true);
                    if (arGuidanceText != null) arGuidanceText.text = "¡Superficie lista! Toca la pantalla para fijar el escenario holográfico.";
                    if (gameplayHudRoot != null) gameplayHudRoot.SetActive(false);
                    break;

                case GameState.Playing:
                    if (arGuidancePanel != null) arGuidancePanel.SetActive(false);
                    // Asegurar que no quede ningún panel de guía huérfano visible
                    foreach (var panel in transform.GetComponentsInChildren<Transform>(true))
                    {
                        if (panel != null && panel.name == "AR_GuidancePanel")
                        {
                            panel.gameObject.SetActive(false);
                        }
                    }
                    if (gameplayHudRoot != null) gameplayHudRoot.SetActive(true);
                    if (startWaveButton != null) startWaveButton.gameObject.SetActive(true);
                    break;

                case GameState.GameOver:
                    if (gameOverPanel != null) gameOverPanel.SetActive(true);
                    if (bossBarContainer != null) bossBarContainer.SetActive(false);
                    break;

                case GameState.Victory:
                    if (victoryPanel != null) victoryPanel.SetActive(true);
                    if (bossBarContainer != null) bossBarContainer.SetActive(false);
                    break;
            }
        }

        private void HandleCoinsChanged(int coins)
        {
            if (coinsText != null)
            {
                coinsText.text = $"{coins} <size=70%>pts</size>";
            }
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (stageHealthText != null)
            {
                stageHealthText.text = $"{current} / {max}";
            }

            if (stageHealthSlider != null)
            {
                stageHealthSlider.maxValue = max;
                stageHealthSlider.value = current;
            }
        }

        private void HandleWaveChanged(int current, int total)
        {
            if (waveText != null)
            {
                waveText.text = current == 0 ? "Preparación" : $"Oleada {current} / {total}";
            }
        }

        private void HandleWaveStarted(int waveNumber)
        {
            if (startWaveButton != null)
            {
                startWaveButton.gameObject.SetActive(false);
            }
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (startWaveButton != null)
                {
                    startWaveButton.gameObject.SetActive(true);
                }
            }
        }

        private void HandleBossSpawned(BossBase boss)
        {
            activeBoss = boss;

            if (bossBarContainer != null)
            {
                bossBarContainer.SetActive(true);
            }

            if (bossNameText != null)
            {
                bossNameText.text = $"JEFE: <color=#FF007F>{boss.BossTitle}</color>";
            }

            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = boss.MaxHealth;
                bossHealthSlider.value = boss.CurrentHealth;
            }

            activeBoss.OnHealthChanged += HandleBossHealthChanged;
        }

        private void HandleBossHealthChanged(float current, float max)
        {
            if (bossHealthSlider != null)
            {
                bossHealthSlider.value = current;
            }
        }

        private void HandleBossDefeated(BossBase boss)
        {
            if (activeBoss == boss)
            {
                activeBoss.OnHealthChanged -= HandleBossHealthChanged;
                activeBoss = null;
            }

            if (bossBarContainer != null)
            {
                bossBarContainer.SetActive(false);
            }
        }

        private void OnStartWaveClicked()
        {
            Debug.Log("<color=#00FFFF><b>[HUDController] ¡INICIAR OLEADA pulsado! Procesando llamada...</b></color>");

            // Asegurar que el estado pase a Playing si el campo ya fue colocado
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                GameManager.Instance.ChangeState(GameState.Playing);
            }

            var ws = WaveSpawner.Instance != null ? WaveSpawner.Instance : FindFirstObjectByType<WaveSpawner>();
            if (ws != null)
            {
                ws.StartNextWave();
            }
            else
            {
                Debug.LogError("[HUDController] No se encontró ninguna instancia de WaveSpawner.");
            }
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }
    }
}
