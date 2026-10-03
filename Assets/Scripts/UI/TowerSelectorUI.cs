using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Core;
using ConcertDefense.Towers;

namespace ConcertDefense.UI
{
    [Serializable]
    public class TowerCardData
    {
        [Tooltip("Tipo de torre de esta tarjeta.")]
        public TowerType type;

        [Tooltip("Nombre de la cantante.")]
        public string characterName;

        [Tooltip("Costo en monedas.")]
        public int cost;

        [Tooltip("Prefab que se construye sobre el BuildSpot.")]
        public GameObject towerPrefab;

        [Tooltip("Botón de la tarjeta.")]
        public Button cardButton;

        [Tooltip("Texto del costo.")]
        public TextMeshProUGUI costText;
    }

    /// <summary>
    /// Selector de torres (GDD 4.3 y 7): aparece al tocar una plataforma libre y muestra una tarjeta
    /// por cantante con su retrato y costo. Las tarjetas se bloquean si no alcanzan las monedas.
    /// </summary>
    public class TowerSelectorUI : MonoBehaviour
    {
        public static TowerSelectorUI Instance { get; private set; }

        [Header("Contenedor")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;

        [Header("Tarjetas (GDD 4.3)")]
        [SerializeField] private TowerCardData[] towerCards = new TowerCardData[0];

        private BuildSpot targetBuildSpot;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            BuildSpot.OnBuildSpotClicked += OpenSelector;
            if (GameManager.Instance != null) GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            if (closeButton != null) closeButton.onClick.AddListener(CloseSelector);

            for (int i = 0; i < towerCards.Length; i++)
            {
                int index = i;
                TowerCardData card = towerCards[i];
                if (card.cardButton != null) card.cardButton.onClick.AddListener(() => OnCardSelected(index));
                if (card.costText != null) card.costText.text = card.cost.ToString();
            }

            CloseSelector();
        }

        private void OnDestroy()
        {
            BuildSpot.OnBuildSpotClicked -= OpenSelector;
            if (GameManager.Instance != null) GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Abre el selector para la plataforma tocada.
        /// </summary>
        public void OpenSelector(BuildSpot spot)
        {
            if (spot == null || spot.IsOccupied) return;

            targetBuildSpot = spot;
            if (TowerMenu.Instance != null) TowerMenu.Instance.CloseMenu();
            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshAffordability();
        }

        /// <summary>
        /// Cierra el selector sin construir.
        /// </summary>
        public void CloseSelector()
        {
            targetBuildSpot = null;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        /// <summary>
        /// Bloquea las tarjetas que el jugador no puede pagar.
        /// </summary>
        public void RefreshAffordability()
        {
            int coins = GameManager.Instance != null ? GameManager.Instance.CurrentCoins : 0;

            foreach (TowerCardData card in towerCards)
            {
                if (card.cardButton != null) card.cardButton.interactable = coins >= card.cost;
            }
        }

        private void HandleCoinsChanged(int coins)
        {
            if (IsOpen) RefreshAffordability();
        }

        private void OnCardSelected(int cardIndex)
        {
            if (targetBuildSpot == null || cardIndex < 0 || cardIndex >= towerCards.Length)
            {
                CloseSelector();
                return;
            }

            if (targetBuildSpot.BuildTower(towerCards[cardIndex].towerPrefab))
            {
                CloseSelector();
            }
            else
            {
                RefreshAffordability();
            }
        }
    }
}
