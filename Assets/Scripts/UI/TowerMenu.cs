using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Towers;
using ConcertDefense.Core;
using ConcertDefense.Player;

namespace ConcertDefense.UI
{
    /// <summary>
    /// Menú flotante interactivo en World Space (GDD 4.3):
    /// - Aparece sobre la torre tocada con orientación Billboard mirando al jugador.
    /// - Muestra información de nivel, daño y alcance.
    /// - Permite Mejorar (si el nivel < 3 y hay monedas) y Vender (con reembolso del 60%).
    /// </summary>
    public class TowerMenu : MonoBehaviour
    {
        public static TowerMenu Instance { get; private set; }

        [Header("Contenedor y Billboard")]
        [Tooltip("Objeto contenedor visual del menú para activarlo u ocultarlo.")]
        [SerializeField] private GameObject menuRoot;

        [Tooltip("Componente UIFollow para posicionar y orientar el menú sobre la torre seleccionada.")]
        [SerializeField] private UIFollow uiFollow;

        [Header("Textos Informativos (TextMeshPro)")]
        [SerializeField] private TextMeshProUGUI towerNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI upgradeCostText;
        [SerializeField] private TextMeshProUGUI sellRefundText;

        [Header("Botones Interactivos")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button closeButton;

        [Header("Desplazamiento")]
        [Tooltip("Altura del menú flotante por encima de la torre.")]
        [SerializeField] private Vector3 menuOffset = new Vector3(0f, 0.45f, 0f);

        private Tower selectedTower;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (uiFollow == null)
            {
                uiFollow = GetComponent<UIFollow>();
            }
        }

        private void OnEnable()
        {
            Tower.OnTowerClicked += OpenMenuForTower;

            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
            if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
            if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);
        }

        private void OnDisable()
        {
            Tower.OnTowerClicked -= OpenMenuForTower;

            if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            if (sellButton != null) sellButton.onClick.RemoveListener(OnSellClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseMenu);
        }

        private void Start()
        {
            CloseMenu();
        }

        /// <summary>
        /// Abre el menú flotante situado sobre la torre tocada y actualiza sus datos.
        /// </summary>
        public void OpenMenuForTower(Tower tower)
        {
            if (tower == null) return;

            // Si ya teníamos otra torre seleccionada, ocultar su retícula de rango
            if (selectedTower != null && selectedTower != tower)
            {
                selectedTower.HideRange();
            }

            selectedTower = tower;
            selectedTower.ShowRange();

            if (uiFollow != null)
            {
                uiFollow.SetTarget(selectedTower.transform, menuOffset);
            }
            else
            {
                transform.position = selectedTower.transform.position + menuOffset;
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(true);
            }

            RefreshDisplay();
        }

        /// <summary>
        /// Actualiza los textos, costos y disponibilidad de botones según el estado de la torre.
        /// </summary>
        public void RefreshDisplay()
        {
            if (selectedTower == null)
            {
                CloseMenu();
                return;
            }

            // 1. Título y Nivel
            if (towerNameText != null)
            {
                towerNameText.text = selectedTower.TowerName;
            }

            if (levelText != null)
            {
                levelText.text = selectedTower.CurrentLevel >= 3 
                    ? "Nivel: <color=#00FFFF>MAX (3)</color>" 
                    : $"Nivel: <color=#00FFFF>{selectedTower.CurrentLevel}/3</color>";
            }

            // 2. Botón y Costo de Mejora
            if (selectedTower.CurrentLevel >= 3)
            {
                if (upgradeCostText != null) upgradeCostText.text = "MAX";
                if (upgradeButton != null) upgradeButton.interactable = false;
            }
            else
            {
                int cost = selectedTower.CurrentUpgradeCost;
                if (upgradeCostText != null) upgradeCostText.text = $"{cost} pts";

                bool canAfford = GameManager.Instance != null && GameManager.Instance.CurrentCoins >= cost;
                if (upgradeButton != null) upgradeButton.interactable = canAfford;
            }

            // 3. Botón y Reembolso de Venta (60% de inversión total)
            int refund = Mathf.RoundToInt(selectedTower.TotalInvestedCoins * 0.6f);
            if (sellRefundText != null)
            {
                sellRefundText.text = $"+{refund} pts";
            }
        }

        /// <summary>
        /// Ejecuta la mejora de la torre seleccionada.
        /// </summary>
        private void OnUpgradeClicked()
        {
            if (selectedTower == null) return;

            bool success = selectedTower.TryUpgrade();
            if (success)
            {
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Ejecuta la venta de la torre seleccionada y libera la plataforma BuildSpot.
        /// </summary>
        private void OnSellClicked()
        {
            if (selectedTower == null) return;

            // Localizar y liberar el BuildSpot padre si existe
            BuildSpot spot = selectedTower.GetComponentInParent<BuildSpot>();
            if (spot != null)
            {
                spot.ClearSpot();
            }

            Tower towerToSell = selectedTower;
            CloseMenu();
            towerToSell.Sell();
        }

        /// <summary>
        /// Cierra el menú y deselecciona la torre.
        /// </summary>
        public void CloseMenu()
        {
            if (selectedTower != null)
            {
                selectedTower.HideRange();
                selectedTower = null;
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }
        }
    }
}
