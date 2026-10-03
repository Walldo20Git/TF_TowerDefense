using System;
using UnityEngine;
using ConcertDefense.Player;
using ConcertDefense.Towers;
using ConcertDefense.UI;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Eventos de toque sobre el mundo 3D con raycast propio (GDD 0 y 2, sustituye a OnMouseDown):
    /// un toque corto sobre una torre abre su menú, sobre una plataforma abre el selector,
    /// sobre un pad teletransporta y sobre el suelo del campo mueve a la heroína.
    /// </summary>
    public class TouchInputRouter : MonoBehaviour
    {
        [Tooltip("Movimiento máximo del dedo, en píxeles, para que cuente como toque y no como arrastre.")]
        [SerializeField] private float tapMaxMovePixels = 28f;

        [Tooltip("Duración máxima del toque en segundos.")]
        [SerializeField] private float tapMaxDuration = 0.6f;

        [Tooltip("Tag del suelo del campo.")]
        [SerializeField] private string fieldTag = "Field";

        private static readonly RaycastHit[] s_Hits = new RaycastHit[24];

        private bool tracking;
        private bool cancelled;
        private Vector2 startPosition;
        private float startTime;

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            {
                tracking = false;
                return;
            }

            // Un segundo dedo convierte el gesto en pellizco/giro, no en toque
            if (PointerInput.PressedCount > 1) cancelled = true;

            if (PointerInput.PrimaryDown(out Vector2 downPos))
            {
                tracking = !PointerInput.IsOverUI(downPos);
                cancelled = false;
                startPosition = downPos;
                startTime = Time.unscaledTime;
                return;
            }

            if (tracking && PointerInput.PrimaryHeld(out Vector2 heldPos))
            {
                if ((heldPos - startPosition).magnitude > tapMaxMovePixels) cancelled = true;
            }

            if (PointerInput.PrimaryUp(out Vector2 upPos))
            {
                bool isTap = tracking && !cancelled && Time.unscaledTime - startTime <= tapMaxDuration;
                tracking = false;
                if (isTap) HandleTap(upPos);
            }
        }

        /// <summary>
        /// Lanza un rayo desde la cámara y entrega el toque al objeto interactivo más cercano.
        /// </summary>
        public void HandleTap(Vector2 screenPosition)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Con el selector de torres abierto, el toque fuera solo lo cierra
            if (TowerSelectorUI.Instance != null && TowerSelectorUI.Instance.IsOpen)
            {
                TowerSelectorUI.Instance.CloseSelector();
                return;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, s_Hits, 50f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(s_Hits, 0, count, RaycastHitDistanceComparer.Instance);

            bool hasFieldPoint = false;
            Vector3 fieldPoint = default;

            for (int i = 0; i < count; i++)
            {
                Collider col = s_Hits[i].collider;

                Tower tower = col.GetComponentInParent<Tower>();
                if (tower != null)
                {
                    tower.Select();
                    return;
                }

                BuildSpot spot = col.GetComponentInParent<BuildSpot>();
                if (spot != null)
                {
                    if (TowerMenu.Instance != null) TowerMenu.Instance.CloseMenu();
                    spot.HandleTap();
                    return;
                }

                TeleportPad pad = col.GetComponentInParent<TeleportPad>();
                if (pad != null)
                {
                    if (TowerMenu.Instance != null) TowerMenu.Instance.CloseMenu();
                    pad.HandleTap();
                    return;
                }

                if (!hasFieldPoint && col.CompareTag(fieldTag))
                {
                    hasFieldPoint = true;
                    fieldPoint = s_Hits[i].point;
                }
            }

            // Toque en zona libre: cierra el menú de torre y mueve a la heroína
            if (TowerMenu.Instance != null) TowerMenu.Instance.CloseMenu();

            if (hasFieldPoint && AvatarController.Instance != null)
            {
                AvatarController.Instance.SetDestination(fieldPoint);
            }
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit a, RaycastHit b)
            {
                return a.distance.CompareTo(b.distance);
            }
        }
    }
}
