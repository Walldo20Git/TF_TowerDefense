using System;
using System.Collections.Generic;
using UnityEngine;
using ConcertDefense.AR;
using ConcertDefense.Core;
using ConcertDefense.Enemies;
using ConcertDefense.Rhythm;

namespace ConcertDefense.Towers
{
    public enum TowerType
    {
        Bass,   // Onda expansiva de daño alto, cadencia lenta (Costo 50)
        Treble, // Notas rápidas, daño bajo, cadencia alta (Costo 40)
        Echo,   // Pulso en área que ralentiza enemigos (Costo 60)
        Drop    // Explosión en área que empuja enemigos con física (Costo 80)
    }

    [Serializable]
    public struct TowerStats
    {
        [Tooltip("Daño por disparo.")]
        public float damage;

        [Tooltip("Alcance en unidades de campo.")]
        public float range;

        [Tooltip("Costo en monedas para subir al siguiente nivel (0 en el último).")]
        public int upgradeCost;
    }

    /// <summary>
    /// Torre-cantante (GDD 4.3 y 4.4):
    /// - Busca al enemigo más avanzado dentro de su alcance y lo apunta.
    /// - Dispara al ritmo: cada torre lanza un proyectil cada N medios tiempos del compás.
    /// - Tres niveles (más daño y alcance), venta con devolución del 60 %.
    /// - Puede quedar silenciada por el jefe Feedback hasta que el avatar la afine.
    /// </summary>
    public class Tower : MonoBehaviour
    {
        /// <summary>Torres construidas. Lo usan los jefes y el avatar.</summary>
        public static readonly List<Tower> All = new List<Tower>();

        /// <summary>Multiplicador de daño global del ritmo (1.5 durante un compás tras un Perfect).</summary>
        public static float RhythmMultiplier = 1f;

        public const int MaxLevel = 3;
        private const float HalfBeatFallback = 0.25f;

        [Header("Tipo e Identidad")]
        [SerializeField] private TowerType towerType = TowerType.Bass;
        [SerializeField] private string towerName = "Bass";
        [SerializeField] private int baseCost = 50;

        [Header("Ritmo")]
        [Tooltip("Dispara una vez cada tantos medios tiempos del compás (1 = muy rápida, 4 = cada tiempo y medio...).")]
        [SerializeField] private int halfBeatsPerShot = 4;

        [Header("Niveles (1, 2 y 3)")]
        [SerializeField] private TowerStats[] statsPerLevel = new TowerStats[MaxLevel]
        {
            new TowerStats { damage = 34f, range = 0.50f, upgradeCost = 60 },
            new TowerStats { damage = 52f, range = 0.58f, upgradeCost = 90 },
            new TowerStats { damage = 80f, range = 0.68f, upgradeCost = 0 }
        };

        [Header("Disparo")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [Tooltip("Parte que gira para mirar al objetivo (la cantante).")]
        [SerializeField] private Transform rotatorPart;
        [Tooltip("Onda visual al disparar.")]
        [SerializeField] private GameObject shotVfxPrefab;

        [Header("Visualización y Estados")]
        [Tooltip("Disco que muestra el alcance al seleccionar la torre.")]
        [SerializeField] private GameObject rangeVisualizer;
        [Tooltip("Aviso visible mientras la torre está silenciada.")]
        [SerializeField] private GameObject silenceIndicator;
        [Tooltip("Luz propia que se enciende si el entorno real está oscuro (modo noche).")]
        [SerializeField] private Light nightLight;

        public TowerType Type => towerType;
        public string TowerName => towerName;
        public int BaseCost => baseCost;
        public int CurrentLevel => currentLevel;
        public int TotalInvestedCoins { get; private set; }
        public bool IsSilenced { get; private set; }
        public BuildSpot Spot { get; set; }

        public float CurrentDamage => GetStats().damage;
        public float CurrentRange => GetStats().range;
        public int CurrentUpgradeCost => GetStats().upgradeCost;
        public int SellRefund => Mathf.RoundToInt(TotalInvestedCoins * 0.6f);

        private int currentLevel = 1;
        private int ticksSinceShot;
        private float fallbackTimer;
        private float silenceTimer;
        private Enemy currentTarget;
        private Vector3 baseRotatorScale = Vector3.one;

        public static event Action<Tower> OnTowerClicked;
        public event Action<int> OnLevelChanged;
        public event Action<bool> OnSilenceStateChanged;

        private void Awake()
        {
            TotalInvestedCoins = baseCost;
            ticksSinceShot = halfBeatsPerShot; // lista para disparar en cuanto haya objetivo
            if (rotatorPart != null) baseRotatorScale = rotatorPart.localScale;
        }

        private void OnEnable()
        {
            All.Add(this);
            BeatClock.OnHalfBeat += HandleHalfBeat;
            LightEstimationController.OnLowLightStateChanged += HandleLowLight;
        }

        private void OnDisable()
        {
            All.Remove(this);
            BeatClock.OnHalfBeat -= HandleHalfBeat;
            LightEstimationController.OnLowLightStateChanged -= HandleLowLight;
        }

        private void Start()
        {
            UpdateLevelVisuals();
            HideRange();
            if (silenceIndicator != null) silenceIndicator.SetActive(false);
            HandleLowLight(LightEstimationController.IsLowLight);
        }

        private void Update()
        {
            if (IsSilenced)
            {
                silenceTimer -= Time.deltaTime;
                if (silenceTimer <= 0f) Tune();
                return;
            }

            UpdateTarget();
            AimAtTarget();

            // Si el reloj de ritmo no está en marcha, se mantiene la misma cadencia con un temporizador
            if (!BeatClock.IsRunning)
            {
                fallbackTimer += Time.deltaTime;
                if (fallbackTimer >= HalfBeatFallback)
                {
                    fallbackTimer -= HalfBeatFallback;
                    HandleHalfBeat(0);
                }
            }
        }

        private void HandleHalfBeat(int tick)
        {
            if (IsSilenced) return;

            ticksSinceShot++;
            if (ticksSinceShot < halfBeatsPerShot) return;
            if (currentTarget == null || currentTarget.IsDead) return;

            ticksSinceShot = 0;
            Shoot();
        }

        /// <summary>
        /// Elige al enemigo más avanzado en el camino dentro del alcance.
        /// </summary>
        private void UpdateTarget()
        {
            float worldRange = CurrentRange * Battlefield.Scale;
            float sqrRange = worldRange * worldRange;
            float bestProgress = float.MinValue;
            Enemy best = null;

            for (int i = 0; i < Enemy.All.Count; i++)
            {
                Enemy enemy = Enemy.All[i];
                if (enemy.IsDead) continue;

                Vector3 delta = enemy.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > sqrRange) continue;

                float progress = enemy.PathProgress;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    best = enemy;
                }
            }

            currentTarget = best;
        }

        private void AimAtTarget()
        {
            if (rotatorPart == null || currentTarget == null) return;

            Vector3 direction = currentTarget.transform.position - rotatorPart.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.000001f) return;

            Quaternion look = Quaternion.LookRotation(direction);
            rotatorPart.rotation = Quaternion.Slerp(rotatorPart.rotation, look, Time.deltaTime * 12f);
        }

        private void Shoot()
        {
            if (projectilePrefab == null || Battlefield.Instance == null) return;

            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 toTarget = currentTarget.transform.position - origin;
            Quaternion rotation = toTarget.sqrMagnitude > 0.000001f ? Quaternion.LookRotation(toTarget) : Quaternion.identity;

            GameObject obj = Instantiate(projectilePrefab, origin, rotation, Battlefield.Instance.Projectiles);
            Projectile projectile = obj.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Launch(currentTarget, CurrentDamage * RhythmMultiplier);
            }

            PulseEffect.Spawn(shotVfxPrefab, origin);
            Sfx.Play(SfxId.Shoot);
        }

        /// <summary>
        /// Sube de nivel si no está al máximo y hay monedas.
        /// </summary>
        public bool TryUpgrade()
        {
            if (currentLevel >= MaxLevel) return false;

            int cost = CurrentUpgradeCost;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendCoins(cost)) return false;

            TotalInvestedCoins += cost;
            currentLevel++;
            UpdateLevelVisuals();
            Sfx.Play(SfxId.Build);
            OnLevelChanged?.Invoke(currentLevel);
            return true;
        }

        /// <summary>
        /// Vende la torre: devuelve el 60 % de lo invertido y libera la plataforma.
        /// </summary>
        public void Sell()
        {
            if (GameManager.Instance != null) GameManager.Instance.AddCoins(SellRefund);
            if (Spot != null) Spot.ClearSpot();

            Sfx.Play(SfxId.Build);
            Destroy(gameObject);
        }

        /// <summary>
        /// Silencia la torre (jefe Feedback): no dispara hasta que el avatar la afine o pase el tiempo.
        /// </summary>
        public void Silence(float duration)
        {
            IsSilenced = true;
            silenceTimer = duration;
            currentTarget = null;

            if (silenceIndicator != null) silenceIndicator.SetActive(true);
            OnSilenceStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Reactiva la torre.
        /// </summary>
        public void Tune()
        {
            if (!IsSilenced) return;

            IsSilenced = false;
            silenceTimer = 0f;

            if (silenceIndicator != null) silenceIndicator.SetActive(false);
            OnSilenceStateChanged?.Invoke(false);
        }

        /// <summary>
        /// La torre fue tocada: muestra su alcance y abre el menú flotante.
        /// </summary>
        public void Select()
        {
            ShowRange();
            OnTowerClicked?.Invoke(this);
        }

        public void ShowRange()
        {
            if (rangeVisualizer != null) rangeVisualizer.SetActive(true);
        }

        public void HideRange()
        {
            if (rangeVisualizer != null) rangeVisualizer.SetActive(false);
        }

        private void UpdateLevelVisuals()
        {
            if (rangeVisualizer != null)
            {
                float diameter = CurrentRange * 2f;
                rangeVisualizer.transform.localScale = new Vector3(diameter, rangeVisualizer.transform.localScale.y, diameter);
            }

            // La cantante crece un poco con cada nivel
            if (rotatorPart != null)
            {
                rotatorPart.localScale = baseRotatorScale * (1f + 0.15f * (currentLevel - 1));
            }
        }

        private void HandleLowLight(bool isDark)
        {
            if (nightLight != null) nightLight.enabled = isDark;
        }

        private TowerStats GetStats()
        {
            int index = Mathf.Clamp(currentLevel - 1, 0, statsPerLevel.Length - 1);
            return statsPerLevel[index];
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, CurrentRange * transform.lossyScale.x);
        }
    }
}
