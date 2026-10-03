using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Core;
using ConcertDefense.Towers;

namespace ConcertDefense.UI
{
    /// <summary>
    /// Menú flotante de torre en World Space (GDD 4.3): aparece sobre la torre tocada mirando a la cámara,
    /// muestra su alcance y permite Mejorar (hasta nivel 3) y Vender (devuelve el 60 %).
    /// </summary>
    public class TowerMenu : MonoBehaviour
    {
        public static TowerMenu Instance { get; private set; }

        [Header("Contenedor y Billboard")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private UIFollow uiFollow;

        [Header("Textos (TextMeshPro)")]
        [SerializeField] private TextMeshProUGUI towerNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI upgradeCostText;
        [SerializeField] private TextMeshProUGUI sellRefundText;

        [Header("Botones")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button closeButton;

        [Header("Posición")]
        [Tooltip("Altura del menú sobre la torre, en unidades de campo.")]
        [SerializeField] private Vector3 menuOffset = new Vector3(0f, 0.42f, 0f);

        private Tower selectedTower;

        public bool IsOpen => selectedTower != null;

        private void Awake()
        {
            Instance = this;
            if (uiFollow == null) uiFollow = GetComponent<UIFollow>();

            // Un Canvas World Space necesita la cámara para recibir toques
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }

        private void Start()
        {
            Tower.OnTowerClicked += OpenMenuForTower;
            if (GameManager.Instance != null) GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;

            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
            if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
            if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);

            CloseMenu();
        }

        private void OnDestroy()
        {
            Tower.OnTowerClicked -= OpenMenuForTower;
            if (GameManager.Instance != null) GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // La torre pudo desaparecer (venta, reinicio)
            if (menuRoot != null && menuRoot.activeSelf && selectedTower == null) CloseMenu();
        }

        /// <summary>
        /// Abre el menú sobre la torre tocada.
        /// </summary>
        public void OpenMenuForTower(Tower tower)
        {
            if (tower == null) return;

            if (selectedTower != null && selectedTower != tower) selectedTower.HideRange();

            selectedTower = tower;
            selectedTower.ShowRange();

            if (uiFollow != null) uiFollow.SetTarget(selectedTower.transform, menuOffset);
            if (menuRoot != null) menuRoot.SetActive(true);

            RefreshDisplay();
        }

        private void HandleCoinsChanged(int coins)
        {
            if (selectedTower != null) RefreshDisplay();
        }

        /// <summary>
        /// Actualiza textos, costos y botones según la torre seleccionada.
        /// </summary>
        public void RefreshDisplay()
        {
            if (selectedTower == null) return;

            bool isMax = selectedTower.CurrentLevel >= Tower.MaxLevel;

            if (towerNameText != null) towerNameText.text = selectedTower.TowerName;
            if (levelText != null) levelText.text = isMax ? "Nivel MAX" : $"Nivel {selectedTower.CurrentLevel} / {Tower.MaxLevel}";
            if (statsText != null)
            {
                statsText.text = $"Daño {selectedTower.CurrentDamage:0}   Alcance {selectedTower.CurrentRange * 100f:0}";
            }

            if (upgradeCostText != null)
            {
                upgradeCostText.text = isMax ? "MEJORAR\nMAX" : $"MEJORAR\n{selectedTower.CurrentUpgradeCost}";
            }

            if (upgradeButton != null)
            {
                bool canAfford = GameManager.Instance != null && GameManager.Instance.CurrentCoins >= selectedTower.CurrentUpgradeCost;
                upgradeButton.interactable = !isMax && canAfford;
            }

            if (sellRefundText != null) sellRefundText.text = $"VENDER\n+{selectedTower.SellRefund}";
        }

        private void OnUpgradeClicked()
        {
            if (selectedTower == null) return;

            // GDD 4.2: mejorar exige tener a la heroína cerca de la plataforma
            if (selectedTower.Spot != null && !selectedTower.Spot.IsAvatarNearby())
            {
                GameMessages.Show("Acerca a la heroína a la torre para mejorarla.");
                return;
            }

            if (selectedTower.TryUpgrade())
            {
                GameMessages.Show($"{selectedTower.TowerName} sube a nivel {selectedTower.CurrentLevel}.", 1.5f);
            }
            RefreshDisplay();
        }

        private void OnSellClicked()
        {
            if (selectedTower == null) return;

            Tower tower = selectedTower;
            CloseMenu();
            GameMessages.Show($"{tower.TowerName} vendida por {tower.SellRefund} monedas.", 1.5f);
            tower.Sell();
        }

        /// <summary>
        /// Cierra el menú y oculta el alcance.
        /// </summary>
        public void CloseMenu()
        {
            if (selectedTower != null) selectedTower.HideRange();
            selectedTower = null;

            if (uiFollow != null) uiFollow.SetTarget(null);
            if (menuRoot != null) menuRoot.SetActive(false);
        }
    }
}
