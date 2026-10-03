using UnityEngine;
using TMPro;
using ConcertDefense.Core;
using ConcertDefense.Enemies;

namespace ConcertDefense.UI
{
    /// <summary>
    /// Barra de vida flotante sobre enemigos y jefes (Canvas World Space + UIFollow, GDD 7).
    /// </summary>
    [RequireComponent(typeof(UIFollow))]
    public class WorldHealthBar : MonoBehaviour
    {
        [Tooltip("Relleno de la barra: se estira según la vida restante.")]
        [SerializeField] private RectTransform fill;

        [Tooltip("Opcional: nombre mostrado sobre la barra (jefes).")]
        [SerializeField] private TextMeshProUGUI nameLabel;

        private Enemy enemy;
        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        /// <summary>
        /// Vincula la barra a un enemigo y la coloca a la altura indicada (unidades de campo).
        /// </summary>
        public void Bind(Enemy target, float height)
        {
            enemy = target;
            transform.localScale = baseScale * Battlefield.Scale;

            if (nameLabel != null)
            {
                BossBase boss = target as BossBase;
                nameLabel.text = boss != null ? boss.BossTitle : target.GlitchName;
            }

            GetComponent<UIFollow>().SetTarget(target.transform, new Vector3(0f, height, 0f));

            enemy.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(enemy.CurrentHealth, enemy.MaxHealth);
        }

        private void OnDestroy()
        {
            if (enemy != null) enemy.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (fill == null) return;

            float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(pct, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}
