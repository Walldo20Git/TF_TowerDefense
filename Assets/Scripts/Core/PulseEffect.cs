using UnityEngine;

namespace ConcertDefense.Core
{
    /// <summary>
    /// Efecto visual ligero (GDD 8): una malla que crece y se desvanece, y luego se destruye.
    /// Se usa para destellos de teletransporte, ondas de disparo, impactos y pulsos de jefe.
    /// </summary>
    public class PulseEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 0.4f;
        [SerializeField] private Vector3 startScale = Vector3.one * 0.05f;
        [SerializeField] private Vector3 endScale = Vector3.one * 0.3f;
        [SerializeField] private Color color = new Color(0f, 1f, 0.95f, 0.8f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float elapsed;
        private float sizeMultiplier = 1f;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            Apply(t);

            if (t >= 1f) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            // Sale rápido y frena al final
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased) * sizeMultiplier;

            Color c = color;
            c.a *= 1f - t;
            block.SetColor(BaseColorId, c);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Crea el efecto dentro del campo para que herede su escala. <paramref name="size"/> multiplica el tamaño.
        /// </summary>
        public static void Spawn(GameObject prefab, Vector3 worldPosition, float size = 1f)
        {
            if (prefab == null) return;

            Transform parent = Battlefield.Instance != null ? Battlefield.Instance.transform : null;
            GameObject obj = Instantiate(prefab, worldPosition, parent != null ? parent.rotation : Quaternion.identity, parent);
            PulseEffect effect = obj.GetComponent<PulseEffect>();
            if (effect != null)
            {
                effect.sizeMultiplier = size;
                effect.Apply(0f);
            }
        }
    }
}
