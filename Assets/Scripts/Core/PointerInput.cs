using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Capa única de entrada (Input System): unifica toques de pantalla y ratón del editor.
    /// Evita depender de OnMouseDown / Input clásico, que fallan si Active Input Handling no es "Both".
    /// </summary>
    public static class PointerInput
    {
        private static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            EnsureEnabled();
        }

        private static void EnsureEnabled()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        private static bool IsPressedPhase(TouchPhase phase)
        {
            return phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
        }

        /// <summary>Número de dedos apoyados ahora mismo (o 1 si el botón izquierdo del ratón está pulsado).</summary>
        public static int PressedCount
        {
            get
            {
                EnsureEnabled();
                var touches = ETouch.activeTouches;
                if (touches.Count > 0)
                {
                    int count = 0;
                    for (int i = 0; i < touches.Count; i++)
                    {
                        if (IsPressedPhase(touches[i].phase)) count++;
                    }
                    return count;
                }

                return Mouse.current != null && Mouse.current.leftButton.isPressed ? 1 : 0;
            }
        }

        /// <summary>Posición del dedo apoyado número <paramref name="index"/> (solo táctil).</summary>
        public static bool TryGetPressedTouch(int index, out Vector2 position)
        {
            EnsureEnabled();
            var touches = ETouch.activeTouches;
            int found = 0;
            for (int i = 0; i < touches.Count; i++)
            {
                if (!IsPressedPhase(touches[i].phase)) continue;
                if (found == index)
                {
                    position = touches[i].screenPosition;
                    return true;
                }
                found++;
            }

            position = default;
            return false;
        }

        /// <summary>True en el fotograma en que empieza un toque de un solo dedo (o clic izquierdo).</summary>
        public static bool PrimaryDown(out Vector2 position)
        {
            EnsureEnabled();
            var touches = ETouch.activeTouches;
            if (touches.Count > 0)
            {
                if (touches.Count == 1 && touches[0].phase == TouchPhase.Began)
                {
                    position = touches[0].screenPosition;
                    return true;
                }
                position = default;
                return false;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>True mientras el dedo principal (o el botón izquierdo) sigue apoyado.</summary>
        public static bool PrimaryHeld(out Vector2 position)
        {
            EnsureEnabled();
            var touches = ETouch.activeTouches;
            if (touches.Count > 0)
            {
                return TryGetPressedTouch(0, out position);
            }

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>True en el fotograma en que se levanta el único dedo (o se suelta el clic).</summary>
        public static bool PrimaryUp(out Vector2 position)
        {
            EnsureEnabled();
            var touches = ETouch.activeTouches;
            if (touches.Count > 0)
            {
                if (touches.Count == 1 && (touches[0].phase == TouchPhase.Ended || touches[0].phase == TouchPhase.Canceled))
                {
                    position = touches[0].screenPosition;
                    return true;
                }
                position = default;
                return false;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        // --- Ayudas solo para pruebas con ratón/teclado en el editor ---

        public static float ScrollDelta => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        public static bool SecondaryHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;

        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        public static bool SpacePressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        public static bool EnterPressed => Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

        public static bool BeatKeyPressed => Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;

        /// <summary>Comprueba si la posición de pantalla cae sobre un elemento de UI (incluye el menú World Space).</summary>
        public static bool IsOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            s_UiHits.Clear();
            EventSystem.current.RaycastAll(eventData, s_UiHits);
            return s_UiHits.Count > 0;
        }
    }
}
