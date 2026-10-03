using UnityEngine;
using ConcertDefense.Core;

namespace ConcertDefense.Enemies
{
    /// <summary>
    /// Jefe de la Oleada 2: Feedback (GDD 5).
    /// Emite un pulso de acople que silencia las torres cercanas. El avatar debe
    /// acercarse (caminando o teletransportándose) para afinarlas y reactivarlas.
    /// </summary>
    public class BossFeedback : BossBase
    {
        [Header("Habilidad: Silenciar Torres")]
        [Tooltip("Radio del pulso, en unidades de campo.")]
        [SerializeField] private float silenceRadius = 0.6f;

        [Tooltip("Segundos que la torre queda en silencio si el avatar no la afina.")]
        [SerializeField] private float silenceDuration = 12f;

        protected override void ExecuteBossAbility()
        {
            int silenced = SilenceTowers(silenceRadius, silenceDuration);
            if (silenced > 0)
            {
                GameMessages.Show("¡Torres silenciadas! Acerca a la heroína para afinarlas.", 3f);
            }
        }
    }
}
