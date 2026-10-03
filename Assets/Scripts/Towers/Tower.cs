using System;
using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.Towers
{
    public enum TowerType
    {
        Bass,   // Onda expansiva de daño alto, cadencia lenta (Costo 50)
        Treble, // Notas musicales rápidas, daño bajo, cadencia alta (Costo 40)
        Echo,   // Pulso en área que ralentiza enemigos (Costo 60)
        Drop    // Explosión en área que empuja enemigos con física (Costo 80)
    }

    [Serializable]
    public struct TowerStats
    {
        [Tooltip("Daño base por disparo.")]
        public float damage;

        [Tooltip("Alcance del radio de ataque en metros.")]
        public float range;

        [Tooltip("Cadencia de disparo (disparos por segundo).")]
        public float fireRate;

        [Tooltip("Costo en monedas para ascender al siguiente nivel.")]
        public int upgradeCost;
    }

    /// <summary>
    /// Componente central de las torres de defensa:
    /// - Detección de enemigos en rango y apuntado rotacional.
    /// - Disparo de proyectiles sincronizable con el ritmo musical.
    /// - Sistema de 3 niveles de mejora y venta con devolución del 60%.
    /// - Estado de silenciado y afinación (mecánica del Jefe Feedback).
    /// </summary>
    public class Tower : MonoBehaviour
    {
        [Header("Tipo e Identidad")]
        [SerializeField] private TowerType towerType = TowerType.Bass;
        [SerializeField] private string towerName = "Bass Tower";
        [SerializeField] private int baseCost = 50;

        [Header("Niveles y Estadísticas (Nivel 1, 2 y 3)")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private TowerStats[] statsPerLevel = new TowerStats[3]
        {
            new TowerStats { damage = 30f, range = 1.2f, fireRate = 1.0f, upgradeCost = 60 },
            new TowerStats { damage = 50f, range = 1.5f, fireRate = 1.2f, upgradeCost = 90 },
            new TowerStats { damage = 85f, range = 1.8f, fireRate = 1.5f, upgradeCost = 0  }
        };

        [Header("Disparo y Puntos de Apuntado")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform rotatorPart;

        [Header("Visualización y Estados")]
        [SerializeField] private GameObject rangeVisualizer;
        [SerializeField] private GameObject silenceIndicator;

        // Variables de estado
        public TowerType Type => towerType;
        public string TowerName => towerName;
        public int CurrentLevel => currentLevel;
        public int TotalInvestedCoins { get; private set; }
        public bool IsSilenced { get; private set; } = false;

        public float CurrentDamage => GetCurrentStats().damage;
        public float CurrentRange => GetCurrentStats().range;
        public float CurrentFireRate => GetCurrentStats().fireRate;
        public int CurrentUpgradeCost => GetCurrentStats().upgradeCost;

        // Multiplicador rítmico (ej: 1.5x en compás Perfect)
        private float rhythmDamageMultiplier = 1f;
        private float fireCountdown = 0f;
        private Transform currentTarget;
        private float silenceTimer = 0f;

        // Eventos
        public static event Action<Tower> OnTowerClicked;
        public event Action<int> OnLevelChanged;
        public event Action<bool> OnSilenceStateChanged;

        private void Awake()
        {
            TotalInvestedCoins = baseCost;
        }

        private void Start()
        {
            UpdateRangeVisualizerScale();
            HideRange();

            if (silenceIndicator != null)
            {
                silenceIndicator.SetActive(false);
            }

            // Buscar objetivos periódicamente en lugar de cada fotograma
            InvokeRepeating(nameof(UpdateTarget), 0f, 0.15f);
        }

        private void Update()
        {
            // Gestión del temporizador de silencio (Jefe Feedback)
            if (IsSilenced)
            {
                silenceTimer -= Time.deltaTime;
                if (silenceTimer <= 0f)
                {
                    Tune();
                }
                return;
            }

            if (currentTarget == null) return;

            // Apuntar hacia el enemigo
            AimAtTarget();

            // Cadencia de disparo
            fireCountdown -= Time.deltaTime;
            if (fireCountdown <= 0f)
            {
                Shoot();
                fireCountdown = 1f / Mathf.Max(0.1f, CurrentFireRate);
            }
        }

        /// <summary>
        /// Localiza al enemigo más cercano dentro del radio de alcance de la torre.
        /// </summary>
        private void UpdateTarget()
        {
            if (IsSilenced) return;

            Collider[] colliders = Physics.OverlapSphere(transform.position, CurrentRange);
            float shortestDistance = Mathf.Infinity;
            Transform nearestEnemy = null;

            foreach (var col in colliders)
            {
                if (col.CompareTag("Enemy"))
                {
                    float distanceToEnemy = Vector3.Distance(transform.position, col.transform.position);
                    if (distanceToEnemy < shortestDistance)
                    {
                        shortestDistance = distanceToEnemy;
                        nearestEnemy = col.transform;
                    }
                }
            }

            currentTarget = nearestEnemy;
        }

        /// <summary>
        /// Rota la cabeza o parte superior de la torre suavemente hacia el enemigo objetivo.
        /// </summary>
        private void AimAtTarget()
        {
            if (rotatorPart == null || currentTarget == null) return;

            Vector3 direction = currentTarget.position - transform.position;
            direction.y = 0f; // Mantener rotación sobre el plano horizontal

            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                rotatorPart.rotation = Quaternion.Slerp(rotatorPart.rotation, lookRotation, Time.deltaTime * 12f);
            }
        }

        /// <summary>
        /// Instancia y dispara el proyectil correspondiente hacia el objetivo actual.
        /// </summary>
        private void Shoot()
        {
            if (projectilePrefab == null || firePoint == null || currentTarget == null) return;

            GameObject projObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            Projectile projectile = projObj.GetComponent<Projectile>();

            if (projectile != null)
            {
                float totalDamage = CurrentDamage * rhythmDamageMultiplier;
                projectile.Initialize(currentTarget, totalDamage);
            }
        }

        /// <summary>
        /// Aplica el multiplicador de daño rítmico (Perfect = 1.5x) durante la ventana del compás.
        /// </summary>
        public void SetRhythmMultiplier(float multiplier)
        {
            rhythmDamageMultiplier = multiplier;
        }

        /// <summary>
        /// Sube la torre de nivel si no ha alcanzado el nivel 3 y el jugador tiene monedas suficientes.
        /// </summary>
        public bool TryUpgrade()
        {
            if (currentLevel >= 3)
            {
                Debug.Log($"[Tower] {towerName} ya está en su nivel máximo (Nivel 3).");
                return false;
            }

            int cost = CurrentUpgradeCost;
            if (GameManager.Instance != null && GameManager.Instance.TrySpendCoins(cost))
            {
                TotalInvestedCoins += cost;
                currentLevel++;
                UpdateRangeVisualizerScale();
                OnLevelChanged?.Invoke(currentLevel);
                Debug.Log($"[Tower] {towerName} mejorada a Nivel {currentLevel}.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Vende la torre devolviendo el 60% de todo lo invertido (compra inicial + mejoras).
        /// </summary>
        public void Sell()
        {
            int refund = Mathf.RoundToInt(TotalInvestedCoins * 0.6f);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(refund);
            }

            Debug.Log($"[Tower] {towerName} vendida por {refund} monedas.");
            Destroy(gameObject);
        }

        /// <summary>
        /// Silencia la torre impidiendo que dispare (habilidad del Jefe Feedback).
        /// </summary>
        public void Silence(float duration)
        {
            IsSilenced = true;
            silenceTimer = duration;

            if (silenceIndicator != null)
            {
                silenceIndicator.SetActive(true);
            }

            OnSilenceStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Reactiva la torre (al afinarla con el avatar o finalizar el tiempo).
        /// </summary>
        public void Tune()
        {
            IsSilenced = false;
            silenceTimer = 0f;

            if (silenceIndicator != null)
            {
                silenceIndicator.SetActive(false);
            }

            OnSilenceStateChanged?.Invoke(false);
            Debug.Log($"[Tower] {towerName} afinada y reactivada.");
        }

        /// <summary>
        /// Muestra la retícula o círculo de alcance.
        /// </summary>
        public void ShowRange()
        {
            if (rangeVisualizer != null)
            {
                rangeVisualizer.SetActive(true);
            }
        }

        /// <summary>
        /// Oculta el círculo de alcance.
        /// </summary>
        public void HideRange()
        {
            if (rangeVisualizer != null)
            {
                rangeVisualizer.SetActive(false);
            }
        }

        private void UpdateRangeVisualizerScale()
        {
            if (rangeVisualizer != null)
            {
                float diameter = CurrentRange * 2f;
                rangeVisualizer.transform.localScale = new Vector3(diameter, rangeVisualizer.transform.localScale.y, diameter);
            }
        }

        private TowerStats GetCurrentStats()
        {
            int index = Mathf.Clamp(currentLevel - 1, 0, statsPerLevel.Length - 1);
            return statsPerLevel[index];
        }

        private void OnMouseDown()
        {
            // Notificar selección al tocar la torre para abrir menú flotante
            ShowRange();
            OnTowerClicked?.Invoke(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, CurrentRange);
        }
    }
}
