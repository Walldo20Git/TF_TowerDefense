using System;
using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Player
{
    /// <summary>
    /// Pad de teletransporte (GDD 4.2):
    /// - Al tocarlo, la heroína desaparece con un destello cian y reaparece sobre este pad.
    /// - Si ya está encima, salta al pad emparejado.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TeleportPad : MonoBehaviour
    {
        [Header("Conexión")]
        [Tooltip("Pad al que se envía a la heroína si ya está sobre este.")]
        [SerializeField] private TeleportPad pairedPad;

        [Header("Ajustes")]
        [Tooltip("Segundos antes de poder volver a usar el pad.")]
        [SerializeField] private float cooldown = 0.5f;

        [Tooltip("Distancia (unidades de campo) a la que se considera que la heroína está sobre el pad.")]
        [SerializeField] private float onPadDistance = 0.12f;

        private float lastUseTime = -10f;

        public bool IsOnCooldown => Time.time < lastUseTime + cooldown;

        public static event Action<TeleportPad> OnTeleportTriggered;

        /// <summary>
        /// Toque del jugador sobre el pad (lo enruta TouchInputRouter).
        /// </summary>
        public void HandleTap()
        {
            if (IsOnCooldown) return;

            AvatarController avatar = AvatarController.Instance;
            if (avatar == null) return;

            Vector3 delta = avatar.transform.position - transform.position;
            delta.y = 0f;
            bool avatarOnPad = delta.magnitude <= onPadDistance * Battlefield.Scale;

            TeleportPad destination = avatarOnPad && pairedPad != null ? pairedPad : this;

            avatar.Teleport(destination.transform.position);
            lastUseTime = Time.time;
            destination.lastUseTime = Time.time;

            OnTeleportTriggered?.Invoke(destination);
        }
    }
}
