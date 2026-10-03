using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ConcertDefense.Enemies;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Configuración de un grupo homogéneo de enemigos dentro de una oleada.
    /// </summary>
    [Serializable]
    public struct EnemyGroup
    {
        [Tooltip("Etiqueta descriptiva del grupo (ej: 8x Pixel).")]
        public string groupName;

        [Tooltip("Prefab del enemigo con el componente Enemy.")]
        public GameObject enemyPrefab;

        [Tooltip("Cantidad total de enemigos de este tipo a instanciar.")]
        public int count;

        [Tooltip("Tiempo en segundos entre la aparición de cada enemigo.")]
        public float spawnInterval;
    }

    /// <summary>
    /// Estructura completa de una oleada con sus grupos de enemigos y jefe final.
    /// </summary>
    [Serializable]
    public class WaveData
    {
        [Tooltip("Nombre de la oleada.")]
        public string waveTitle;

        [Tooltip("Grupos de enemigos normales que aparecerán en orden.")]
        public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();

        [Tooltip("Prefab del Jefe de la oleada.")]
        public GameObject bossPrefab;

        [Tooltip("Pausa en segundos tras el último glitch común antes de la llegada del jefe.")]
        public float delayBeforeBoss = 4f;
    }

    /// <summary>
    /// Controla el flujo de oleadas (GDD 6): crea enemigos y jefe, y cierra la oleada
    /// cuando el jefe y todos los glitches han salido del campo.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        public static WaveSpawner Instance { get; private set; }

        [Header("Configuración de Oleadas (GDD 6)")]
        [SerializeField] private List<WaveData> waves = new List<WaveData>();

        public bool IsWaveInProgress { get; private set; }
        public int CurrentWaveIndex { get; private set; } // 0-indexed
        public int TotalWaves => waves.Count;
        public int ActiveEnemies => activeEnemies;

        private int activeEnemies;
        private Transform[] waypoints;

        public event Action<int> OnWaveStarted;             // (número de oleada 1-based)
        public event Action<int> OnWaveCompleted;           // (número de oleada superada)
        public event Action<int> OnRemainingEnemiesChanged; // (enemigos en el campo)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Inicia la siguiente oleada. Lo llama el botón "Iniciar oleada" del HUD.
        /// Devuelve false si todavía no se puede (campo sin colocar, oleada en curso...).
        /// </summary>
        public bool StartNextWave()
        {
            if (IsWaveInProgress) return false;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
            if (CurrentWaveIndex >= waves.Count) return false;

            Battlefield field = Battlefield.Instance;
            if (field == null || !field.gameObject.activeInHierarchy) return false;

            waypoints = field.GetPathWaypoints();
            if (waypoints.Length < 2)
            {
                Debug.LogError("[WaveSpawner] El Battlefield no tiene waypoints en 'Path'.");
                return false;
            }

            StartCoroutine(WaveRoutine(CurrentWaveIndex));
            return true;
        }

        private IEnumerator WaveRoutine(int waveIndex)
        {
            IsWaveInProgress = true;
            activeEnemies = 0;

            int waveNumber = waveIndex + 1;
            WaveData wave = waves[waveIndex];

            GameManager.Instance.SetWave(waveNumber);
            OnWaveStarted?.Invoke(waveNumber);
            Debug.Log($"[WaveSpawner] Oleada {waveNumber}/{waves.Count}: {wave.waveTitle}");

            // 1. Glitches normales, grupo a grupo
            foreach (EnemyGroup group in wave.enemyGroups)
            {
                for (int i = 0; i < group.count; i++)
                {
                    if (!GameManager.Instance.IsPlaying) yield break;
                    SpawnEnemy(group.enemyPrefab);
                    yield return new WaitForSeconds(Mathf.Max(0.1f, group.spawnInterval));
                }
            }

            // 2. Jefe de la oleada
            if (wave.bossPrefab != null)
            {
                yield return new WaitForSeconds(wave.delayBeforeBoss);
                if (!GameManager.Instance.IsPlaying) yield break;
                SpawnEnemy(wave.bossPrefab);
            }

            // 3. Esperar a que el jefe y el resto de glitches salgan del campo
            while (activeEnemies > 0)
            {
                yield return null;
            }

            if (!GameManager.Instance.IsPlaying) yield break;

            // 4. Oleada superada: pausa para construir
            IsWaveInProgress = false;
            CurrentWaveIndex++;
            OnWaveCompleted?.Invoke(waveNumber);

            if (CurrentWaveIndex >= waves.Count)
            {
                GameManager.Instance.ChangeState(GameState.Victory);
            }
        }

        private void SpawnEnemy(GameObject prefab)
        {
            if (prefab == null || Battlefield.Instance == null) return;

            GameObject obj = Instantiate(prefab, Battlefield.Instance.Enemies);
            Enemy enemy = obj.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError($"[WaveSpawner] El prefab {prefab.name} no tiene componente Enemy.");
                Destroy(obj);
                return;
            }

            enemy.InitializePath(waypoints, 0, null);
            RegisterEnemy(enemy);
        }

        /// <summary>
        /// Cuenta a un enemigo como parte de la oleada (también los esbirros que crean los jefes).
        /// </summary>
        public void RegisterEnemy(Enemy enemy)
        {
            if (enemy == null) return;

            activeEnemies++;
            enemy.OnRemoved += HandleEnemyRemoved;
            OnRemainingEnemiesChanged?.Invoke(activeEnemies);
        }

        private void HandleEnemyRemoved(Enemy enemy)
        {
            enemy.OnRemoved -= HandleEnemyRemoved;
            activeEnemies = Mathf.Max(0, activeEnemies - 1);
            OnRemainingEnemiesChanged?.Invoke(activeEnemies);
        }
    }
}
