using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ConcertDefense.Rhythm
{
    /// <summary>
    /// Reenvía el momento exacto en que se apoya el dedo. Button.onClick se dispara al soltar,
    /// y ese retraso arruinaría la precisión del ritmo.
    /// </summary>
    public class PointerDownRelay : MonoBehaviour, IPointerDownHandler
    {
        public event Action OnDown;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDown?.Invoke();
        }
    }
}
