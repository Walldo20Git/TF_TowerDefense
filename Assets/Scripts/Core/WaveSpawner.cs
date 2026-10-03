using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ConcertDefense.Enemies;
using ConcertDefense.AR;

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
        [Tooltip("Nombre de la oleada (ej: Oleada 1 - Introducción al Ruido).")]
        public string waveTitle;

        [Tooltip("Grupos de enemigos normales que aparecerán en orden.")]
        public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();

        [Tooltip("Prefab del Jefe de la oleada (opcional en oleadas sin jefe).")]
        public GameObject bossPrefab;

        [Tooltip("Pausa en segundos tras el último glitch común antes de la llegada del jefe.")]
        public float delayBeforeBoss = 3f;
    }

    /// <summary>
    /// Controlador responsable del flujo, temporización y generación de oleadas de enemigos.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        public static WaveSpawner Instance { get; private set; }

        [Header("Configuración de Oleadas (GDD)")]
        [Tooltip("Lista secuencial de oleadas definidas para la partida.")]
        [SerializeField] private List<WaveData> waves = new List<WaveData>();

        [Header("Referencias de Ruta y Contenedor")]
        [Tooltip("Transform padre que contiene los waypoints ordenados como hijos.")]
        [SerializeField] private Transform pathContainer;

        [Tooltip("Lista manual de waypoints (si no se usa un contenedor padre).")]
        [SerializeField] private Transform[] waypoints;

        [Tooltip("Contenedor donde se agruparán los enemigos instanciados en la jerarquía.")]
        [SerializeField] private Transform enemiesContainer;

        // Variables de estado
        public bool IsWaveInProgress { get; private set; } = false;
        public int CurrentWaveIndex { get; private set; } = 0; // 0-indexed
        public int TotalWaves => waves.Count > 0 ? waves.Count : 4;

        private int activeEnemiesCount = 0;
        private Coroutine waveCoroutine;
        private FieldManipulator fieldManipulator;

        // Eventos
        public event Action<int> OnWaveStarted;             // (número de oleada 1-based)
        public event Action<int> OnWaveCompleted;           // (número de oleada superada)
        public event Action<int> OnRemainingEnemiesChanged; // (enemigos restantes en combate)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            fieldManipulator = FindFirstObjectByType<FieldManipulator>();
        }

        private void Start()
        {
            ExtractWaypoints();
        }

        /// <summary>
        /// Asigna y extrae los waypoints desde el objeto Battlefield recién colocado.
        /// </summary>
        public void SetupPathAndContainers(Transform pathParent, Transform enemiesParent)
        {
            pathContainer = pathParent;
            enemiesContainer = enemiesParent;
            ExtractWaypoints();
        }

        /// <summary>
        /// Extrae automáticamente los hijos del contenedor Path como waypoints ordenados.
        /// </summary>
        private void ExtractWaypoints()
        {
            if (pathContainer == null)
            {
                GameObject foundPath = GameObject.Find("Path");
                if (foundPath != null) pathContainer = foundPath.transform;
            }

            if (enemiesContainer == null)
            {
                GameObject foundEnemies = GameObject.Find("EnemiesContainer");
                if (foundEnemies != null) enemiesContainer = foundEnemies.transform;
            }

            if (pathContainer != null && pathContainer.childCount > 0)
            {
                waypoints = new Transform[pathContainer.childCount];
                for (int i = 0; i < pathContainer.childCount; i++)
                {
                    waypoints[i] = pathContainer.GetChild(i);
                }
            }
        }

        /// <summary>
        /// Inicia la siguiente oleada. Llamado típicamente desde el botón 'Iniciar Oleada' de la UI.
        /// </summary>
        public void StartNextWave()
        {
            if (IsWaveInProgress)
            {
                Debug.LogWarning("[WaveSpawner] Ya hay una oleada en combate.");
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                // Si el escenario ya está presente en la escena, pasar a Playing
                var placer = FindFirstObjectByType<ARPlacementController>();
                bool isBattlefieldPresent = (placer != null && placer.IsPlaced) ||
                                            GameObject.Find("Battlefield") != null ||
                                            GameObject.Find("Battlefield(Clone)") != null ||
                                            GameObject.Find("Path") != null ||
                                            (waypoints != null && waypoints.Length > 0);

                if (isBattlefieldPresent)
                {
                    GameManager.Instance.ChangeState(GameState.Playing);
                }
                else
                {
                    Debug.LogWarning("[WaveSpawner] No se puede iniciar oleada sin haber colocado el escenario. Pulsa Espacio para colocarlo.");
                    return;
                }
            }

            if (CurrentWaveIndex >= TotalWaves)
            {
                Debug.Log("[WaveSpawner] ¡Todas las oleadas han sido completadas!");
                GameManager.Instance?.ChangeState(GameState.Victory);
                return;
            }

            // Si los waypoints aún no estaban cacheados, intentar extraerlos
            if (waypoints == null || waypoints.Length == 0)
            {
                ExtractWaypoints();
            }

            if (waypoints == null || waypoints.Length == 0)
            {
                Debug.LogError("[WaveSpawner] No hay waypoints configurados para la ruta de los enemigos.");
                return;
            }

            Debug.Log($"<color=#00FF88><b>[WaveSpawner] ¡Iniciando Oleada {CurrentWaveIndex + 1}/{TotalWaves}!</b></color>");
            waveCoroutine = StartCoroutine(SpawnWaveRoutine(CurrentWaveIndex));
        }

        /// <summary>
        /// Corrutina que gestiona la aparición secuencial de los enemigos de la oleada actual.
        /// </summary>
        private IEnumerator SpawnWaveRoutine(int waveIndex)
        {
            IsWaveInProgress = true;
            activeEnemiesCount = 0;

            // Bloquear manipulación del campo durante el combate para no desorientar el juego
            if (fieldManipulator != null)
            {
                fieldManipulator.ManipulationAllowed = false;
            }

            int waveDisplayNumber = waveIndex + 1;
            GameManager.Instance?.SetWave(waveDisplayNumber);
            OnWaveStarted?.Invoke(waveDisplayNumber);

            WaveData currentWave = waveIndex < waves.Count ? waves[waveIndex] : null;

            if (currentWave != null)
            {
                // 1. Instanciar grupos de enemigos normales
                foreach (var group in currentWave.enemyGroups)
                {
                    for (int i = 0; i < group.count; i++)
                    {
                        SpawnEnemy(group.enemyPrefab);
                        yield return new WaitForSeconds(group.spawnInterval);
                    }
                }

                // 2. Si hay un jefe definido, pausar brevemente y luego instanciarlo
                if (currentWave.bossPrefab != null)
                {
                    yield return new WaitForSeconds(currentWave.delayBeforeBoss);
                    SpawnEnemy(currentWave.bossPrefab);
                }
            }
            else
            {
                Debug.LogWarning($"[WaveSpawner] No hay configuración de datos para la oleada {waveDisplayNumber}.");
            }

            // 3. Esperar hasta que todos los enemigos y el jefe sean derrotados o alcancen la meta
            while (activeEnemiesCount > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // 4. Oleada completada con éxito
            IsWaveInProgress = false;
            OnWaveCompleted?.Invoke(waveDisplayNumber);

            // Reactivar manipulación y descanso para construir
            if (fieldManipulator != null)
            {
                fieldManipulator.ManipulationAllowed = true;
            }

            CurrentWaveIndex++;

            if (CurrentWaveIndex >= TotalWaves)
            {
                GameManager.Instance?.ChangeState(GameState.Victory);
            }
            else
            {
                Debug.Log($"[WaveSpawner] Oleada {waveDisplayNumber} superada. Pausa activa para construir torres.");
            }
        }

        /// <summary>
        /// Instancia un enemigo individual en el primer waypoint y se suscribe a sus eventos de vida.
        /// </summary>
        private void SpawnEnemy(GameObject prefab)
        {
            if (prefab == null) return;

            Vector3 spawnPos = waypoints.Length > 0 ? waypoints[0].position : transform.position;
            Quaternion spawnRot = waypoints.Length > 0 ? waypoints[0].rotation : Quaternion.identity;

            Transform parent = enemiesContainer != null ? enemiesContainer : transform;
            GameObject enemyObj = Instantiate(prefab, spawnPos, spawnRot, parent);

            Enemy enemy = enemyObj.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.InitializePath(waypoints);
                activeEnemiesCount++;
                OnRemainingEnemiesChanged?.Invoke(activeEnemiesCount);

                // Suscripción a eventos de ciclo de vida
                enemy.OnEnemyDeath += HandleEnemyFinished;
                enemy.OnEnemyReachedEnd += HandleEnemyFinished;
            }
        }

        /// <summary>
        /// Decrementa el contador de enemigos activos al morir o alcanzar la meta.
        /// </summary>
        private void HandleEnemyFinished(Enemy enemy)
        {
            if (enemy != null)
            {
                enemy.OnEnemyDeath -= HandleEnemyFinished;
                enemy.OnEnemyReachedEnd -= HandleEnemyFinished;
            }

            activeEnemiesCount = Mathf.Max(0, activeEnemiesCount - 1);
            OnRemainingEnemiesChanged?.Invoke(activeEnemiesCount);
        }
    }
}
