using System;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Canal de avisos cortos para el jugador. Cualquier sistema publica y el HUD los muestra.
    /// </summary>
    public static class GameMessages
    {
        public static event Action<string, float> OnMessage;

        public static void Show(string text, float seconds = 2.5f)
        {
            OnMessage?.Invoke(text, seconds);
        }
    }
}
