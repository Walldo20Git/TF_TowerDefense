using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ConcertDefense.Towers;
using ConcertDefense.Core;

namespace ConcertDefense.UI
{
    [Serializable]
    public class TowerCardData
    {
        [Tooltip("Tipo de torre correspondiente a esta tarjeta.")]
        public TowerType type;

        [Tooltip("Nombre de la cantante/personaje anime de la torre.")]
        public string characterName;

        [Tooltip("Costo en monedas para construir esta torre.")]
        public int cost;

        [Tooltip("Prefab que se instanciará sobre el BuildSpot.")]
        public GameObject towerPrefab;

        [Tooltip("Botón táctil de la tarjeta.")]
        public Button cardButton;

        [Tooltip("Texto para desplegar el costo en monedas.")]
        public TextMeshProUGUI costText;
    }

    /// <summary>
    /// Menú de selección y compra de torres (GDD 4.3 y 7):
    /// - Aparece en pantalla cuando el jugador pulsa sobre una plataforma libre (BuildSpot).
    /// - Muestra las tarjetas con retrato anime y costo de cada torre (Bass: 50, Treble: 40, Echo: 60, Drop: 80).
    /// - Habilita o deshabilita los botones según el saldo de monedas disponible.
    /// - Al seleccionar una tarjeta, construye la torre sobre la plataforma activa y cierra el menú.
    /// </summary>
    public class TowerSelectorUI : MonoBehaviour
    {
        public static TowerSelectorUI Instance { get; private set; }

        [Header("Contenedor Principal")]
        [Tooltip("Panel raíz que contiene las tarjetas de compra de torres.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Botón para cancelar y cerrar el selector sin construir.")]
        [SerializeField] private Button closeButton;

        [Header("Tarjetas de Personaje / Torre (GDD 4.3)")]
        [SerializeField] private TowerCardData[] towerCards;

        // Referencia a la plataforma seleccionada actualmente
        private BuildSpot targetBuildSpot;

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
            BuildSpot.OnBuildSpotClicked += OpenSelector;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSelector);
            }

            // Configurar listeners de cada tarjeta
            for (int i = 0; i < towerCards.Length; i++)
            {
                int index = i;
                if (towerCards[index].cardButton != null)
                {
                    towerCards[index].cardButton.onClick.AddListener(() => OnCardSelected(index));
                }
            }
        }

        private void OnDisable()
        {
            BuildSpot.OnBuildSpotClicked -= OpenSelector;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseSelector);
            }

            for (int i = 0; i < towerCards.Length; i++)
            {
                if (towerCards[i].cardButton != null)
                {
                    towerCards[i].cardButton.onClick.RemoveAllListeners();
                }
            }
        }

        private void Start()
        {
            InitializeCardTexts();
            CloseSelector();
        }

        /// <summary>
        /// Asigna los textos de costo iniciales a cada tarjeta.
        /// </summary>
        private void InitializeCardTexts()
        {
            foreach (var card in towerCards)
            {
                if (card.costText != null)
                {
                    card.costText.text = $"{card.cost} pts";
                }
            }
        }

        /// <summary>
        /// Despliega el menú de selección vinculado al BuildSpot presionado.
        /// </summary>
        public void OpenSelector(BuildSpot spot)
        {
            if (spot == null || spot.IsOccupied) return;

            targetBuildSpot = spot;

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            RefreshAffordability();
        }

        /// <summary>
        /// Oculta el menú selector y desvincula la plataforma.
        /// </summary>
        public void CloseSelector()
        {
            targetBuildSpot = null;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Comprueba si el jugador puede costear cada una de las 4 torres y actualiza la interactividad de los botones.
        /// </summary>
        public void RefreshAffordability()
        {
            int currentCoins = GameManager.Instance != null ? GameManager.Instance.CurrentCoins : 0;

            foreach (var card in towerCards)
            {
                if (card.cardButton != null)
                {
                    card.cardButton.interactable = currentCoins >= card.cost;
                }
            }
        }

        private void HandleCoinsChanged(int newCoins)
        {
            if (panelRoot != null && panelRoot.activeSelf)
            {
                RefreshAffordability();
            }
        }

        /// <summary>
        /// Llamado cuando el jugador toca una tarjeta para construir la torre elegida.
        /// </summary>
        private void OnCardSelected(int cardIndex)
        {
            if (targetBuildSpot == null)
            {
                CloseSelector();
                return;
            }

            if (cardIndex < 0 || cardIndex >= towerCards.Length) return;

            TowerCardData chosenCard = towerCards[cardIndex];

            // Intentar construir la torre en la plataforma
            bool built = targetBuildSpot.BuildTower(chosenCard.towerPrefab);

            if (built)
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
