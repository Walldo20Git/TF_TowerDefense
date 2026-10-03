using UnityEngine;

namespace ConcertDefense.AR
{
    /// <summary>
    /// Retícula de colocación (GDD 7): anillo turquesa con un aro exterior que se pone
    /// verde cuando el punto es válido y rojo cuando no.
    /// </summary>
    public class PlacementReticle : MonoBehaviour
    {
        [Tooltip("Material sin iluminación con propiedad _BaseColor.")]
        [SerializeField] private Material ringMaterial;

        [Tooltip("Radio del aro exterior en metros (aprox. medio campo).")]
        [SerializeField] private float outerRadius = 0.48f;

        [SerializeField] private Color baseColor = new Color(0f, 0.95f, 0.9f, 0.9f);
        [SerializeField] private Color validColor = new Color(0.1f, 1f, 0.4f, 0.9f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.25f, 0.9f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer outerRenderer;
        private MaterialPropertyBlock block;
        private bool built;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();

            outerRenderer = CreateRing("OuterRing", outerRadius * 0.94f, outerRadius);
            Renderer inner = CreateRing("CenterRing", 0.035f, 0.055f);

            block.SetColor(BaseColorId, baseColor);
            inner.SetPropertyBlock(block);
            SetValid(false);
        }

        /// <summary>
        /// Cambia el color del aro exterior: verde (válido) o rojo (inválido).
        /// </summary>
        public void SetValid(bool valid)
        {
            Build();
            block.SetColor(BaseColorId, valid ? validColor : invalidColor);
            outerRenderer.SetPropertyBlock(block);
        }

        private Renderer CreateRing(string ringName, float inner, float outer)
        {
            var obj = new GameObject(ringName);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(0f, 0.004f, 0f);

            obj.AddComponent<MeshFilter>().sharedMesh = BuildRingMesh(inner, outer, 48);
            var meshRenderer = obj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = ringMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return meshRenderer;
        }

        /// <summary>
        /// Genera una corona circular plana sobre el plano XZ.
        /// </summary>
        private static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir * outer;
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "ReticleRing" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
