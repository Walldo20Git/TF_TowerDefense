using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.Rhythm
{
    /// <summary>
    /// Componente físico del carrito de la montaña rusa que barre el campo dañando enemigos al impactar.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RollerCoasterCart : MonoBehaviour
    {
        private float damage = 250f;
        private float knockback = 15f;
        private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        public void Initialize(float cartDamage, float cartKnockback)
        {
            damage = cartDamage;
            knockback = cartKnockback;
            hitEnemies.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Enemy"))
            {
                Enemy enemy = other.GetComponent<Enemy>();
                if (enemy != null && !hitEnemies.Contains(enemy))
                {
                    hitEnemies.Add(enemy);
                    enemy.TakeDamage(damage);

                    Vector3 pushDir = transform.forward + Vector3.up * 0.4f;
                    enemy.ApplyKnockback(pushDir.normalized * knockback);
                }
            }
        }
    }

    /// <summary>
    /// Controlador del ataque especial 'Roller Coaster' (GDD 4.5):
    /// - Acumula energía del medidor con aciertos rítmicos (RhythmInput) y recarga pasiva.
    /// - Habilita el botón de activación con efecto de brillo cuando el medidor llega al 100%.
    /// - Conduce el carrito de montaña rusa a lo largo de las vías atropellando a los glitches.
    /// </summary>
    public class UltimateController : MonoBehaviour
    {
        public static UltimateController Instance { get; private set; }

        [Header("Interfaz Screen Space")]
        [Tooltip("Botón para disparar el ataque especial cuando la carga esté completa.")]
        [SerializeField] private Button ultimateButton;

        [Tooltip("Imagen radial o lineal con Fill Amount (0 a 1) que muestra la carga del Ultimate.")]
        [SerializeField] private Image chargeFillImage;

        [Tooltip("Efecto de resplandor activo cuando el Ultimate está al 100%.")]
        [SerializeField] private GameObject readyVisualEffect;

        [Header("Montaña Rusa y Recorrido")]
        [Tooltip("GameObject del carrito de la montaña rusa (hijo de Battlefield).")]
        [SerializeField] private GameObject cartObject;

        [Tooltip("Contenedor padre que almacena los waypoints de la vía en orden.")]
        [SerializeField] private Transform trackContainer;

        [Tooltip("Waypoints manuales de la vía si no se usa contenedor padre.")]
        [SerializeField] private Transform[] trackWaypoints;

        [Tooltip("Velocidad de avance del carrito por las vías.")]
        [SerializeField] private float cartSpeed = 6.5f;

        [Tooltip("Daño masivo causado a cada enemigo que impacta el carrito.")]
        [SerializeField] private float cartDamage = 300f;

        [Tooltip("Fuerza de empuje físico al arrollar glitches.")]
        [SerializeField] private float knockbackForce = 15f;

        [Header("Ajustes de Carga")]
        [Tooltip("Tasa de recarga pasiva por segundo (0.01 = 1% por segundo).")]
        [SerializeField] private float passiveChargeRate = 0.015f;

        // Estado
        private float currentCharge = 0f;
        private bool isCartActive = false;
        private Coroutine cartRoutine;
        private RollerCoasterCart cartComponent;

        public float CurrentCharge => currentCharge;
        public bool IsReady => currentCharge >= 1.0f;
        public bool IsCartActive => isCartActive;

        // Eventos
        public event Action<float> OnChargeChanged;
        public event Action OnUltimateReady;
        public event Action OnUltimateExecuted;

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
            RhythmInput.OnUltimateChargeGenerated += AddCharge;

            if (ultimateButton != null)
            {
                ultimateButton.onClick.AddListener(TriggerUltimate);
            }
        }

        private void OnDisable()
        {
            RhythmInput.OnUltimateChargeGenerated -= AddCharge;

            if (ultimateButton != null)
            {
                ultimateButton.onClick.RemoveListener(TriggerUltimate);
            }
        }

        private void Start()
        {
            ExtractTrackWaypoints();

            if (cartObject != null)
            {
                cartComponent = cartObject.GetComponent<RollerCoasterCart>();
                if (cartComponent == null)
                {
                    cartComponent = cartObject.AddComponent<RollerCoasterCart>();
                }
                cartObject.SetActive(false);
            }

            UpdateUI();
        }

        private void Update()
        {
            // Recarga pasiva gradual durante la partida activa
            if (!IsReady && !isCartActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                AddCharge(passiveChargeRate * Time.deltaTime);
            }
        }

        /// <summary>
        /// Asigna y extrae las vías del Roller Coaster desde el Battlefield instanciado.
        /// </summary>
        public void SetupTrack(Transform trackParent, GameObject cart)
        {
            trackContainer = trackParent;
            cartObject = cart;

            if (cartObject != null)
            {
                cartComponent = cartObject.GetComponent<RollerCoasterCart>();
                if (cartComponent == null)
                {
                    cartComponent = cartObject.AddComponent<RollerCoasterCart>();
                }
                cartObject.SetActive(false);
            }

            ExtractTrackWaypoints();
        }

        private void ExtractTrackWaypoints()
        {
            if (trackContainer == null)
            {
                GameObject foundTrack = GameObject.Find("RollerCoasterTrack");
                if (foundTrack != null) trackContainer = foundTrack.transform;
            }

            if (cartObject == null)
            {
                cartObject = GameObject.Find("RollerCoasterCart");
                if (cartObject != null && cartComponent == null)
                {
                    cartComponent = cartObject.GetComponent<RollerCoasterCart>();
                    if (cartComponent == null) cartComponent = cartObject.AddComponent<RollerCoasterCart>();
                }
            }

            if (trackContainer != null && trackContainer.childCount > 0)
            {
                trackWaypoints = new Transform[trackContainer.childCount];
                for (int i = 0; i < trackContainer.childCount; i++)
                {
                    trackWaypoints[i] = trackContainer.GetChild(i);
                }
            }
        }

        /// <summary>
        /// Añade carga al medidor de Ultimate (generada por aciertos de ritmo o pasiva).
        /// </summary>
        public void AddCharge(float amount)
        {
            if (isCartActive) return;

            bool wasReady = IsReady;
            currentCharge = Mathf.Clamp01(currentCharge + amount);

            if (!wasReady && IsReady)
            {
                OnUltimateReady?.Invoke();
            }

            OnChargeChanged?.Invoke(currentCharge);
            UpdateUI();
        }

        /// <summary>
        /// Activa el ataque especial si la carga está completa y el carrito no está en uso.
        /// </summary>
        public void TriggerUltimate()
        {
            if (!IsReady || isCartActive) return;

            if (trackWaypoints == null || trackWaypoints.Length == 0)
            {
                ExtractTrackWaypoints();
            }

            if (cartObject == null || trackWaypoints == null || trackWaypoints.Length < 2)
            {
                Debug.LogWarning("[UltimateController] No hay vía o carrito configurado para el Roller Coaster.");
                return;
            }

            currentCharge = 0f;
            UpdateUI();

            OnUltimateExecuted?.Invoke();

            if (cartRoutine != null)
            {
                StopCoroutine(cartRoutine);
            }
            cartRoutine = StartCoroutine(RollerCoasterRoutine());
        }

        /// <summary>
        /// Corrutina que traslada el carrito por cada waypoint de la montaña rusa a alta velocidad.
        /// </summary>
        private IEnumerator RollerCoasterRoutine()
        {
            isCartActive = true;

            if (cartComponent != null)
            {
                cartComponent.Initialize(cartDamage, knockbackForce);
            }

            cartObject.SetActive(true);
            cartObject.transform.position = trackWaypoints[0].position;
            cartObject.transform.rotation = trackWaypoints[0].rotation;

            int targetIndex = 1;

            while (targetIndex < trackWaypoints.Length)
            {
                Transform targetWaypoint = trackWaypoints[targetIndex];
                if (targetWaypoint == null) break;

                while (Vector3.Distance(cartObject.transform.position, targetWaypoint.position) > 0.05f)
                {
                    // Orientar y mover hacia el punto
                    Vector3 dir = (targetWaypoint.position - cartObject.transform.position).normalized;
                    if (dir != Vector3.zero)
                    {
                        cartObject.transform.rotation = Quaternion.Slerp(cartObject.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 18f);
                    }

                    cartObject.transform.position = Vector3.MoveTowards(cartObject.transform.position, targetWaypoint.position, cartSpeed * Time.deltaTime);
                    yield return null;
                }

                targetIndex++;
            }

            // Al completar la vía, ocultar el carrito
            cartObject.SetActive(false);
            isCartActive = false;
            cartRoutine = null;
            UpdateUI();

            Debug.Log("[UltimateController] Ataque Roller Coaster finalizado.");
        }

        private void UpdateUI()
        {
            if (chargeFillImage != null)
            {
                chargeFillImage.fillAmount = currentCharge;
            }

            if (ultimateButton != null)
            {
                ultimateButton.interactable = IsReady && !isCartActive;
            }

            if (readyVisualEffect != null)
            {
                readyVisualEffect.SetActive(IsReady && !isCartActive);
            }
        }
    }
}
