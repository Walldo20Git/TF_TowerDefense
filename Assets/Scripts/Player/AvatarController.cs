using System;
using UnityEngine;
using ConcertDefense.Core;
using ConcertDefense.Towers;

namespace ConcertDefense.Player
{
    /// <summary>
    /// Heroína del jugador (GDD 4.2):
    /// - Camina hacia el punto tocado del campo con MoveTowards (sin NavMesh), en coordenadas locales del campo.
    /// - Se teletransporta entre TeleportPads con destello cian.
    /// - Afina las torres silenciadas por el jefe Feedback al acercarse.
    /// </summary>
    public class AvatarController : MonoBehaviour
    {
        public static AvatarController Instance { get; private set; }

        [Header("Movimiento (GDD 4.2)")]
        [Tooltip("Velocidad de caminata en unidades de campo por segundo.")]
        [SerializeField] private float moveSpeed = 0.6f;

        [Tooltip("Velocidad de giro hacia el destino.")]
        [SerializeField] private float rotationSpeed = 14f;

        [Tooltip("Mitad del lado del campo: la heroína no sale de este cuadrado.")]
        [SerializeField] private float fieldHalfSize = 0.78f;

        [Header("Efectos")]
        [Tooltip("Destello cian del teletransporte.")]
        [SerializeField] private GameObject teleportVfxPrefab;

        [Tooltip("Marca visual en el punto de destino.")]
        [SerializeField] private GameObject moveIndicatorPrefab;

        [Tooltip("Parte visual que se balancea al caminar.")]
        [SerializeField] private Transform visualRoot;

        [Header("Afinación de Torres (Jefe Feedback)")]
        [Tooltip("Distancia a la que afina una torre silenciada, en unidades de campo.")]
        [SerializeField] private float tuneProximity = 0.35f;

        private Vector3 targetLocalPosition;
        private bool isMoving;
        private float baseLocalY;
        private float tuneCheckTimer;

        public bool IsMoving => isMoving;

        public event Action<Vector3> OnDestinationSet;
        public event Action OnArrivedAtDestination;
        public event Action<Vector3> OnTeleported;

        private void Awake()
        {
            Instance = this;
            targetLocalPosition = transform.localPosition;
            baseLocalY = transform.localPosition.y;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            MoveTowardsTarget();
            AnimateWalk();

            tuneCheckTimer -= Time.deltaTime;
            if (tuneCheckTimer <= 0f)
            {
                tuneCheckTimer = 0.25f;
                TuneNearbyTowers();
            }
        }

        /// <summary>
        /// Fija el destino a partir de un punto del mundo tocado sobre el campo.
        /// </summary>
        public void SetDestination(Vector3 worldPoint)
        {
            targetLocalPosition = ToClampedLocal(worldPoint);
            isMoving = true;

            PulseEffect.Spawn(moveIndicatorPrefab, LocalToWorld(targetLocalPosition) - Vector3.up * (baseLocalY * Battlefield.Scale * 0.9f));
            OnDestinationSet?.Invoke(worldPoint);
        }

        /// <summary>
        /// Camina hasta quedarse a <paramref name="stopDistance"/> (unidades de campo) del punto indicado.
        /// </summary>
        public void SetDestinationNear(Vector3 worldPoint, float stopDistance)
        {
            Vector3 local = ToClampedLocal(worldPoint);
            Vector3 fromTarget = transform.localPosition - local;
            fromTarget.y = 0f;

            if (fromTarget.magnitude > stopDistance)
            {
                local += fromTarget.normalized * stopDistance;
            }
            else
            {
                local = transform.localPosition;
            }

            SetDestination(LocalToWorld(local));
        }

        private void MoveTowardsTarget()
        {
            if (!isMoving) return;

            Vector3 current = transform.localPosition;
            Vector3 toTarget = targetLocalPosition - current;
            toTarget.y = 0f;

            if (toTarget.magnitude <= 0.01f)
            {
                isMoving = false;
                OnArrivedAtDestination?.Invoke();
                return;
            }

            Quaternion look = Quaternion.LookRotation(toTarget.normalized);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, look, Time.deltaTime * rotationSpeed);
            transform.localPosition = Vector3.MoveTowards(current, targetLocalPosition, moveSpeed * Time.deltaTime);
        }

        private void AnimateWalk()
        {
            if (visualRoot == null) return;

            // Pequeño salto al caminar y respiración suave en reposo
            float bob = isMoving ? Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.025f : Mathf.Sin(Time.time * 2.5f) * 0.006f;
            visualRoot.localPosition = new Vector3(0f, bob, 0f);
        }

        /// <summary>
        /// Teletransporta a la heroína a un punto del mundo (lo usa TeleportPad).
        /// </summary>
        public void Teleport(Vector3 worldPoint)
        {
            PulseEffect.Spawn(teleportVfxPrefab, transform.position);

            Vector3 local = ToClampedLocal(worldPoint);
            transform.localPosition = local;
            targetLocalPosition = local;
            isMoving = false;

            PulseEffect.Spawn(teleportVfxPrefab, transform.position);
            Sfx.Play(SfxId.Teleport);
            OnTeleported?.Invoke(transform.position);
        }

        /// <summary>
        /// Afina las torres silenciadas cercanas (mecánica del jefe Feedback).
        /// </summary>
        private void TuneNearbyTowers()
        {
            float worldRadius = tuneProximity * Battlefield.Scale;

            for (int i = 0; i < Tower.All.Count; i++)
            {
                Tower tower = Tower.All[i];
                if (!tower.IsSilenced) continue;

                Vector3 delta = tower.transform.position - transform.position;
                delta.y = 0f;
                if (delta.magnitude > worldRadius) continue;

                tower.Tune();
                PulseEffect.Spawn(teleportVfxPrefab, tower.transform.position, 0.7f);
                Sfx.Play(SfxId.Good);
                GameMessages.Show($"¡Torre {tower.TowerName} afinada!", 1.5f);
            }
        }

        private Vector3 ToClampedLocal(Vector3 worldPoint)
        {
            Transform parent = transform.parent;
            Vector3 local = parent != null ? parent.InverseTransformPoint(worldPoint) : worldPoint;
            local.x = Mathf.Clamp(local.x, -fieldHalfSize, fieldHalfSize);
            local.z = Mathf.Clamp(local.z, -fieldHalfSize, fieldHalfSize);
            local.y = baseLocalY;
            return local;
        }

        private Vector3 LocalToWorld(Vector3 local)
        {
            Transform parent = transform.parent;
            return parent != null ? parent.TransformPoint(local) : local;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, tuneProximity * transform.lossyScale.x);
        }
    }
}
