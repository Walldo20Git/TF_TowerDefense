using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Estados principales del bucle de juego según el GDD.
    /// </summary>
    public enum GameState
    {
        Scanning,    // Escaneando el entorno para detectar planos AR
        Placing,     // Plano encontrado; retícula activa lista para fijar el escenario
        Playing,     // Escenario fijado; juego activo (preparación y combate de oleadas)
        GameOver,    // El Ánimo del escenario llegó a 0
        Victory      // Se derrotaron todos los jefes de las 4 oleadas
    }

    /// <summary>
    /// Administrador central del estado del juego, economía (monedas), salud del escenario ("Ánimo") y oleadas.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Configuración Inicial")]
        [Tooltip("Vida inicial del escenario ('Ánimo'). Por defecto 20.")]
        [SerializeField] private int initialHealth = 20;

        [Tooltip("Monedas iniciales para construir torres. Por defecto 150.")]
        [SerializeField] private int initialCoins = 150;

        [Tooltip("Número total de oleadas del juego.")]
        [SerializeField] private int totalWaves = 4;

        public GameState CurrentState { get; private set; } = GameState.Scanning;
        public bool IsPlaying => CurrentState == GameState.Playing;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => initialHealth;
        public int CurrentCoins { get; private set; }
        public int CurrentWave { get; private set; } = 0;
        public int TotalWaves => totalWaves;

        // Eventos para notificar a la UI y otros sistemas sin acoplamiento
        public event Action<GameState> OnGameStateChanged;
        public event Action<int, int> OnHealthChanged; // (actual, máxima)
        public event Action<int> OnCoinsChanged;       // (monedas actuales)
        public event Action<int, int> OnWaveChanged;   // (oleada actual, total)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CurrentHealth = initialHealth;
            CurrentCoins = initialCoins;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Cambia el estado del juego y dispara el evento de notificación.
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            // Una partida terminada solo se abandona reiniciando la escena
            if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory) return;

            CurrentState = newState;
            OnGameStateChanged?.Invoke(CurrentState);

            if (CurrentState == GameState.GameOver)
            {
                Debug.Log("[GameManager] Fin de la partida: ¡Game Over!");
            }
            else if (CurrentState == GameState.Victory)
            {
                Debug.Log("[GameManager] ¡Victoria! Todos los glitches y jefes han sido derrotados.");
            }
        }

        /// <summary>
        /// Añade monedas al jugador (al eliminar enemigos, vender torres, etc.).
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            CurrentCoins += amount;
            OnCoinsChanged?.Invoke(CurrentCoins);
        }

        /// <summary>
        /// Intenta gastar monedas (para construir o mejorar torres).
        /// Devuelve true si la compra fue exitosa, o false si no alcanzan los fondos.
        /// </summary>
        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (CurrentCoins < amount) return false;

            CurrentCoins -= amount;
            OnCoinsChanged?.Invoke(CurrentCoins);
            return true;
        }

        /// <summary>
        /// Aplica daño al Ánimo del escenario cuando un glitch o jefe alcanza la meta.
        /// </summary>
        public void TakeStageDamage(int damage)
        {
            if (CurrentState != GameState.Playing) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Sfx.Play(SfxId.StageHit);

            if (CurrentHealth <= 0)
            {
                ChangeState(GameState.GameOver);
            }
        }

        /// <summary>
        /// Registra el número de oleada en curso.
        /// </summary>
        public void SetWave(int waveNumber)
        {
            CurrentWave = waveNumber;
            OnWaveChanged?.Invoke(CurrentWave, totalWaves);
        }

        /// <summary>
        /// Reinicia la escena actual para volver a jugar tras un Game Over o Victoria.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
