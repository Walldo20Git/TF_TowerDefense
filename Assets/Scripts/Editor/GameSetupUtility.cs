using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using ConcertDefense.AR;
using ConcertDefense.Core;
using ConcertDefense.Enemies;
using ConcertDefense.Player;
using ConcertDefense.Rhythm;
using ConcertDefense.Towers;
using ConcertDefense.UI;

namespace ConcertDefense.EditorTools
{
    /// <summary>
    /// Montaje automático del juego: genera materiales, audio, prefabs, HUD y deja la escena lista.
    /// Menú: ConcertDefense → Setup Complete Game. Se puede volver a ejecutar sin duplicar nada.
    /// </summary>
    public static class GameSetupUtility
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MaterialsPath = "Assets/Materials";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string AudioPath = "Assets/Audio";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Paleta (GDD 8): turquesa, cian, negro y blanco con acentos magenta; glitches en rojo y negro
        private static readonly Color Turquoise = new Color(0f, 0.85f, 0.8f);
        private static readonly Color IceCyan = new Color(0.6f, 0.95f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.15f, 0.6f);
        private static readonly Color Ink = new Color(0.07f, 0.08f, 0.12f);
        private static readonly Color White = new Color(0.95f, 0.97f, 1f);
        private static readonly Color Skin = new Color(1f, 0.86f, 0.76f);
        private static readonly Color GlitchRed = new Color(0.95f, 0.1f, 0.15f);
        private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.12f, 0.88f);

        private static Dictionary<string, Material> mats;
        private static Dictionary<string, GameObject> vfx;
        private static TMP_FontAsset font;
        private static Sprite roundedSprite;
        private static Sprite circleSprite;

        private struct TowerDef
        {
            public TowerType type;
            public string name;
            public string description;
            public int cost;
            public int halfBeats;
            public Color hair;
            public Color dress;
            public Color accent;
            public GameObject projectile;
            public TowerStats[] stats;
            public GameObject prefab;
        }

        [MenuItem("ConcertDefense/Setup Complete Game (Generate All Assets & Scene)", priority = 1)]
        public static void SetupCompleteGame()
        {
            Debug.Log("[ConcertDefense] Iniciando montaje completo del juego...");

            CreateDirectories();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            Dictionary<SfxId, AudioClip> sfxClips = GenerateAudio(out AudioClip bgm);
            CreateMaterials();
            CreateVfxPrefabs();

            GameObject enemyBar = CreateHealthBarPrefab("EnemyHealthBar", new Vector2(100f, 12f), false);
            GameObject bossBar = CreateHealthBarPrefab("BossHealthBar", new Vector2(220f, 18f), true);

            Dictionary<string, GameObject> enemies = CreateEnemies(enemyBar, bossBar);
            TowerDef[] towers = CreateTowers();
            GameObject avatar = CreateAvatarPrefab();
            GameObject reticle = CreateReticlePrefab();
            GameObject battlefield = CreateBattlefieldPrefab(avatar);

            AssembleScene(bgm, sfxClips, battlefield, reticle, towers, enemies);
            ApplyProjectSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ConcertDefense] Montaje completado: materiales, audio, prefabs, HUD y escena configurados.");
        }

        private static void CreateDirectories()
        {
            string[] dirs =
            {
                MaterialsPath, AudioPath, PrefabsPath,
                PrefabsPath + "/Towers", PrefabsPath + "/Projectiles", PrefabsPath + "/Enemies",
                PrefabsPath + "/Bosses", PrefabsPath + "/Environment", PrefabsPath + "/VFX", PrefabsPath + "/UI"
            };

            foreach (string dir in dirs)
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace("\\", "/"), Path.GetFileName(dir));
            }
        }

        #region Utilidades

        /// <summary>Asigna campos [SerializeField] por nombre.</summary>
        private static void SetProps(UnityEngine.Object target, params (string name, object value)[] props)
        {
            var so = new SerializedObject(target);
            foreach ((string name, object value) in props)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null)
                {
                    Debug.LogError($"[ConcertDefense] {target.GetType().Name} no tiene el campo '{name}'.");
                    continue;
                }

                switch (value)
                {
                    case null: p.objectReferenceValue = null; break;
                    case bool b: p.boolValue = b; break;
                    case int i: p.intValue = i; break;
                    case float f: p.floatValue = f; break;
                    case string s: p.stringValue = s; break;
                    case Color c: p.colorValue = c; break;
                    case Vector2 v2: p.vector2Value = v2; break;
                    case Vector3 v3: p.vector3Value = v3; break;
                    case Enum e: p.enumValueIndex = Convert.ToInt32(e); break;
                    case UnityEngine.Object o: p.objectReferenceValue = o; break;
                    default: Debug.LogError($"[ConcertDefense] Tipo no soportado para '{name}'."); break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Empty(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        /// <summary>Primitiva visual sin collider.</summary>
        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>Barra fina entre dos puntos (raíles, soportes, tramos del camino).</summary>
        private static GameObject Segment(string name, Transform parent, Vector3 a, Vector3 b, float width, float height, Material mat)
        {
            Vector3 delta = b - a;
            GameObject go = Prim(PrimitiveType.Cube, name, parent, (a + b) * 0.5f, new Vector3(width, height, delta.magnitude), mat);
            if (delta.sqrMagnitude > 0.000001f) go.transform.localRotation = Quaternion.LookRotation(delta.normalized);
            return go;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        #endregion

        #region Materiales

        private static void CreateMaterials()
        {
            mats = new Dictionary<string, Material>();

            Shader toon = Shader.Find("ConcertDefense/Toon");
            Shader unlit = Shader.Find("ConcertDefense/UnlitColor");
            if (toon == null) toon = Shader.Find("Universal Render Pipeline/Lit");
            if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");

            Material Create(string name, Shader shader, Color baseColor, Color emission)
            {
                string path = $"{MaterialsPath}/{name}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    mat.shader = shader;
                }

                mat.SetColor("_BaseColor", baseColor);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                mats[name] = mat;
                return mat;
            }

            void Toon(string name, Color color, float glow = 0f) => Create(name, toon, color, color * glow);
            void Unlit(string name, Color color) => Create(name, unlit, color, Color.black);

            Toon("Mat_Turquoise", Turquoise, 0.25f);
            Toon("Mat_IceCyan", IceCyan, 0.2f);
            Toon("Mat_Magenta", Magenta, 0.25f);
            Toon("Mat_Ink", Ink);
            Toon("Mat_White", White, 0.1f);
            Toon("Mat_Skin", Skin, 0.1f);
            Toon("Mat_LeekGreen", new Color(0.35f, 0.85f, 0.3f), 0.2f);
            Toon("Mat_GlitchRed", GlitchRed, 0.35f);
            Toon("Mat_GlitchBlack", new Color(0.09f, 0.03f, 0.05f));
            Toon("Mat_Ground", new Color(0.09f, 0.11f, 0.18f));
            Toon("Mat_Path", new Color(0.2f, 0.16f, 0.32f), 0.15f);

            Unlit("Mat_Grid", new Color(0f, 0.95f, 0.9f, 0.3f));
            Unlit("Mat_Range", new Color(0f, 0.95f, 0.9f, 0.18f));
            Unlit("Mat_Indicator", new Color(0.1f, 1f, 0.45f, 0.85f));
            Unlit("Mat_Reticle", new Color(0f, 0.95f, 0.9f, 0.9f));
            Unlit("Mat_Noise", new Color(0.75f, 0.1f, 0.9f, 0.35f));
            Unlit("Mat_Fx", new Color(1f, 1f, 1f, 0.8f));
            Unlit("Mat_Hologram", new Color(0.2f, 1f, 0.95f, 0.45f));

            // Los materiales del avance anterior ya no se usan
            foreach (string old in new[]
            {
                "Mat_BossDark", "Mat_BuildSpotInRange", "Mat_BuildSpotNormal", "Mat_BuildSpotOutOfRange", "Mat_DarkStage",
                "Mat_EnemyAmp", "Mat_EnemyPixel", "Mat_EnemyStatic", "Mat_FieldPath", "Mat_NeonCyan", "Mat_NeonMagenta",
                "Mat_NeonPurple", "Mat_NeonYellow", "Mat_NoiseZone", "Mat_WhiteMetal"
            })
            {
                AssetDatabase.DeleteAsset($"{MaterialsPath}/{old}.mat");
            }
        }

        #endregion

        #region Audio

        private static Dictionary<SfxId, AudioClip> GenerateAudio(out AudioClip bgm)
        {
            const int rate = 44100;
            var noise = new System.Random(7);

            // --- Pista principal: 120 BPM, 16 tiempos (8 s) en bucle, de autoría propia ---
            float beat = 0.5f;
            var music = new float[(int)(rate * beat * 16)];
            float[] bass = { 110f, 110f, 130.81f, 130.81f, 146.83f, 146.83f, 98f, 98f };
            float[] lead = { 440f, 523.25f, 659.25f, 523.25f, 587.33f, 659.25f, 783.99f, 659.25f, 440f, 523.25f, 659.25f, 880f, 783.99f, 659.25f, 587.33f, 523.25f };

            for (int i = 0; i < music.Length; i++)
            {
                float t = (float)i / rate;
                float beatTime = t % beat;
                int beatIndex = (int)(t / beat);

                float kickFreq = Mathf.Lerp(150f, 45f, Mathf.Clamp01(beatTime / 0.12f));
                float kick = Mathf.Sin(2f * Mathf.PI * kickFreq * beatTime) * Mathf.Exp(-beatTime * 14f) * 0.75f;

                float hatTime = t % (beat * 0.5f);
                float hat = ((float)noise.NextDouble() * 2f - 1f) * Mathf.Exp(-hatTime * 50f) * 0.14f;

                float bassFreq = bass[(beatIndex / 2) % bass.Length];
                float bassNote = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * Mathf.Exp(-beatTime * 3f) * 0.32f;

                float leadFreq = lead[beatIndex % lead.Length];
                float leadNote = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * leadFreq * t)) * Mathf.Exp(-beatTime * 6f) * 0.07f;

                music[i] = Mathf.Clamp(kick + hat + bassNote + leadNote, -1f, 1f);
            }

            bgm = SaveClip("BGM_120BPM_CyberConcert.wav", music, rate);

            // --- Efectos ---
            float[] Sweep(float from, float to, float duration, float volume, float noiseMix = 0f, float tremolo = 0f)
            {
                var data = new float[(int)(rate * duration)];
                double phase = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    float t = (float)i / data.Length;
                    float freq = Mathf.Lerp(from, to, t);
                    phase += 2.0 * Math.PI * freq / rate;
                    float env = Mathf.Sin(t * Mathf.PI);
                    float tone = (float)Math.Sin(phase);
                    float n = (float)noise.NextDouble() * 2f - 1f;
                    float trem = tremolo > 0f ? 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * tremolo * t * duration) : 1f;
                    data[i] = Mathf.Lerp(tone, n, noiseMix) * env * trem * volume;
                }
                return data;
            }

            float[] Concat(params float[][] parts) => parts.SelectMany(p => p).ToArray();

            var clips = new Dictionary<SfxId, AudioClip>
            {
                [SfxId.Shoot] = SaveClip("ShootSFX.wav", Sweep(900f, 500f, 0.07f, 0.35f), rate),
                [SfxId.Impact] = SaveClip("ImpactSFX.wav", Sweep(220f, 90f, 0.1f, 0.45f, 0.5f), rate),
                [SfxId.Build] = SaveClip("BuildSFX.wav", Concat(Sweep(523f, 523f, 0.08f, 0.5f), Sweep(659f, 659f, 0.08f, 0.5f), Sweep(784f, 784f, 0.12f, 0.5f)), rate),
                [SfxId.Teleport] = SaveClip("TeleportSFX.wav", Sweep(600f, 1400f, 0.3f, 0.55f), rate),
                [SfxId.Perfect] = SaveClip("PerfectSFX.wav", Concat(Sweep(1318f, 1318f, 0.06f, 0.5f), Sweep(1760f, 1760f, 0.12f, 0.5f)), rate),
                [SfxId.Good] = SaveClip("GoodSFX.wav", Sweep(880f, 880f, 0.1f, 0.45f), rate),
                [SfxId.BossArrive] = SaveClip("BossArriveSFX.wav", Sweep(130f, 65f, 0.8f, 0.7f, 0.15f, 9f), rate),
                [SfxId.Feedback] = SaveClip("FeedbackSFX.wav", Sweep(1800f, 2600f, 0.5f, 0.4f, 0.1f, 14f), rate),
                [SfxId.Ultimate] = SaveClip("UltimateSFX.wav", Sweep(180f, 1500f, 0.7f, 0.6f, 0.1f), rate),
                [SfxId.StageHit] = SaveClip("StageHitSFX.wav", Sweep(160f, 60f, 0.28f, 0.7f, 0.25f), rate)
            };
            return clips;
        }

        private static AudioClip SaveClip(string fileName, float[] samples, int sampleRate)
        {
            string path = $"{AudioPath}/{fileName}";
            byte[] wav = new byte[44 + samples.Length * 2];

            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            BitConverter.GetBytes(wav.Length - 8).CopyTo(wav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            BitConverter.GetBytes(16).CopyTo(wav, 16);
            BitConverter.GetBytes((short)1).CopyTo(wav, 20);  // PCM
            BitConverter.GetBytes((short)1).CopyTo(wav, 22);  // Mono
            BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
            BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28);
            BitConverter.GetBytes((short)2).CopyTo(wav, 32);
            BitConverter.GetBytes((short)16).CopyTo(wav, 34);
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            BitConverter.GetBytes(samples.Length * 2).CopyTo(wav, 40);

            for (int i = 0; i < samples.Length; i++)
            {
                short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                BitConverter.GetBytes(value).CopyTo(wav, 44 + i * 2);
            }

            File.WriteAllBytes(path, wav);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        #endregion

        #region Efectos visuales

        private static void CreateVfxPrefabs()
        {
            vfx = new Dictionary<string, GameObject>();

            void Create(string name, PrimitiveType shape, Vector3 start, Vector3 end, float duration, Color color)
            {
                var root = new GameObject(name);
                Prim(shape, "Mesh", root.transform, Vector3.zero, Vector3.one, mats["Mat_Fx"]);
                PulseEffect effect = root.AddComponent<PulseEffect>();
                SetProps(effect, ("duration", duration), ("startScale", start), ("endScale", end), ("color", color));
                vfx[name] = SavePrefab(root, $"{PrefabsPath}/VFX/{name}.prefab");
            }

            Vector3 Disc(float diameter) => new Vector3(diameter, 0.004f, diameter);

            Create("Vfx_CyanFlash", PrimitiveType.Sphere, Vector3.one * 0.05f, Vector3.one * 0.34f, 0.45f, new Color(0.2f, 1f, 1f, 0.85f));
            Create("Vfx_Shot", PrimitiveType.Sphere, Vector3.one * 0.03f, Vector3.one * 0.12f, 0.2f, new Color(1f, 1f, 1f, 0.6f));
            Create("Vfx_Impact", PrimitiveType.Sphere, Vector3.one * 0.03f, Vector3.one * 0.11f, 0.22f, new Color(0.3f, 1f, 0.95f, 0.8f));
            Create("Vfx_Death", PrimitiveType.Cube, Vector3.one * 0.06f, Vector3.one * 0.2f, 0.35f, new Color(1f, 0.15f, 0.2f, 0.85f));
            Create("Vfx_MagentaPulse", PrimitiveType.Cylinder, Disc(0.1f), Disc(1f), 0.6f, new Color(1f, 0.15f, 0.6f, 0.6f));
            Create("Vfx_MoveMarker", PrimitiveType.Cylinder, Disc(0.14f), Disc(0.03f), 0.5f, new Color(0.2f, 1f, 1f, 0.8f));
        }

        #endregion

        #region Barras de vida (World Space)

        private static GameObject CreateHealthBarPrefab(string name, Vector2 size, bool withLabel)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rootRT = (RectTransform)root.transform;
            rootRT.sizeDelta = size;
            rootRT.localScale = Vector3.one * 0.0012f;

            RectTransform bg = UIRect("Background", root.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIImage(bg, new Color(0.02f, 0.02f, 0.05f, 0.85f), null, false);

            RectTransform area = UIRect("FillArea", root.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));
            RectTransform fill = UIRect("Fill", area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIImage(fill, withLabel ? Magenta : GlitchRed, null, false);

            TextMeshProUGUI label = null;
            if (withLabel)
            {
                RectTransform labelRT = UIRect("Name", root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(60f, 26f));
                label = UIText(labelRT, "Jefe", 20f, White, TextAlignmentOptions.Center);
            }

            UIFollow follow = root.AddComponent<UIFollow>();
            SetProps(follow, ("destroyWithTarget", true), ("keepConstantScreenSize", false), ("lockYAxisOnly", false));

            WorldHealthBar bar = root.AddComponent<WorldHealthBar>();
            SetProps(bar, ("fill", fill), ("nameLabel", label));

            return SavePrefab(root, $"{PrefabsPath}/UI/{name}.prefab");
        }

        #endregion

        #region Personajes

        /// <summary>
        /// Cantante chibi original hecha con primitivas: coletas largas y micrófono (GDD 1, nota de contenido).
        /// Altura aproximada 1 unidad antes de aplicar <paramref name="scale"/>; mira hacia +Z.
        /// </summary>
        private static Transform BuildChibi(Transform parent, string name, Material hair, Material dress, Material accent, float scale)
        {
            Transform root = Empty(name, parent, Vector3.zero).transform;
            root.localScale = Vector3.one * scale;

            Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.32f, 0.28f, 0.32f), dress);
            Prim(PrimitiveType.Cylinder, "Skirt", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.05f, 0.5f), accent);
            Prim(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 0.74f, 0f), Vector3.one * 0.44f, mats["Mat_Skin"]);
            Prim(PrimitiveType.Sphere, "Hair", root, new Vector3(0f, 0.8f, -0.06f), Vector3.one * 0.47f, hair);
            Prim(PrimitiveType.Capsule, "TwinTail_L", root, new Vector3(-0.3f, 0.45f, -0.08f), new Vector3(0.11f, 0.36f, 0.11f), hair, new Vector3(0f, 0f, -14f));
            Prim(PrimitiveType.Capsule, "TwinTail_R", root, new Vector3(0.3f, 0.45f, -0.08f), new Vector3(0.11f, 0.36f, 0.11f), hair, new Vector3(0f, 0f, 14f));
            Prim(PrimitiveType.Sphere, "Eye_L", root, new Vector3(-0.09f, 0.74f, 0.19f), Vector3.one * 0.07f, mats["Mat_Ink"]);
            Prim(PrimitiveType.Sphere, "Eye_R", root, new Vector3(0.09f, 0.74f, 0.19f), Vector3.one * 0.07f, mats["Mat_Ink"]);
            Prim(PrimitiveType.Cylinder, "Mic", root, new Vector3(0.2f, 0.48f, 0.2f), new Vector3(0.05f, 0.08f, 0.05f), mats["Mat_Ink"], new Vector3(25f, 0f, -15f));
            Prim(PrimitiveType.Sphere, "MicHead", root, new Vector3(0.23f, 0.57f, 0.24f), Vector3.one * 0.09f, mats["Mat_White"]);
            return root;
        }

        private static GameObject CreateAvatarPrefab()
        {
            var root = new GameObject("Avatar_Hero") { tag = "Avatar" };
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.05f;
            col.height = 0.22f;
            col.center = new Vector3(0f, 0.11f, 0f);

            Transform visual = Empty("Visual", root.transform, Vector3.zero).transform;
            BuildChibi(visual, "Heroine", mats["Mat_Turquoise"], mats["Mat_Ink"], mats["Mat_Magenta"], 0.22f);

            // Aro a los pies para distinguirla de las torres
            Prim(PrimitiveType.Cylinder, "FootRing", root.transform, new Vector3(0f, 0.004f, 0f), new Vector3(0.13f, 0.002f, 0.13f), mats["Mat_Hologram"]);

            AvatarController avatar = root.AddComponent<AvatarController>();
            SetProps(avatar,
                ("moveSpeed", 0.6f), ("fieldHalfSize", 0.78f), ("tuneProximity", 0.35f),
                ("teleportVfxPrefab", vfx["Vfx_CyanFlash"]), ("moveIndicatorPrefab", vfx["Vfx_MoveMarker"]), ("visualRoot", visual));

            return SavePrefab(root, $"{PrefabsPath}/Environment/Avatar_Hero.prefab");
        }

        private static GameObject CreateReticlePrefab()
        {
            var root = new GameObject("PlacementReticle");
            PlacementReticle reticle = root.AddComponent<PlacementReticle>();
            SetProps(reticle, ("ringMaterial", mats["Mat_Reticle"]), ("outerRadius", 0.5f));
            return SavePrefab(root, $"{PrefabsPath}/Environment/PlacementReticle.prefab");
        }

        #endregion

        #region Proyectiles y torres

        private static GameObject CreateProjectile(string name, Action<Transform> buildVisual, float speed, float splash, float knockback, float slow, float slowDuration)
        {
            var root = new GameObject(name);
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.04f;
            col.isTrigger = true;

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            buildVisual(root.transform);

            Projectile projectile = root.AddComponent<Projectile>();
            SetProps(projectile,
                ("speed", speed), ("splashRadius", splash), ("knockbackForce", knockback),
                ("slowFactor", slow), ("slowDuration", slowDuration), ("impactVfxPrefab", vfx["Vfx_Impact"]));

            return SavePrefab(root, $"{PrefabsPath}/Projectiles/{name}.prefab");
        }

        private static TowerDef[] CreateTowers()
        {
            // Proyectiles: onda de bajos, puerro (guiño visual), pulso de eco y bloque de "drop"
            GameObject bassProj = CreateProjectile("Projectile_Bass",
                t => Prim(PrimitiveType.Sphere, "Wave", t, Vector3.zero, Vector3.one * 0.07f, mats["Mat_Turquoise"]),
                2.2f, 0.1f, 0f, 1f, 0f);

            GameObject trebleProj = CreateProjectile("Projectile_Treble",
                t =>
                {
                    Prim(PrimitiveType.Capsule, "LeekStalk", t, new Vector3(0f, 0f, -0.012f), new Vector3(0.018f, 0.03f, 0.018f), mats["Mat_LeekGreen"], new Vector3(90f, 0f, 0f));
                    Prim(PrimitiveType.Capsule, "LeekBulb", t, new Vector3(0f, 0f, 0.03f), new Vector3(0.02f, 0.018f, 0.02f), mats["Mat_White"], new Vector3(90f, 0f, 0f));
                },
                3.4f, 0f, 0f, 1f, 0f);

            GameObject echoProj = CreateProjectile("Projectile_Echo",
                t => Prim(PrimitiveType.Cylinder, "Pulse", t, Vector3.zero, new Vector3(0.11f, 0.008f, 0.11f), mats["Mat_IceCyan"], new Vector3(90f, 0f, 0f)),
                2.2f, 0.22f, 0f, 0.5f, 1.8f);

            GameObject dropProj = CreateProjectile("Projectile_Drop",
                t =>
                {
                    Prim(PrimitiveType.Cube, "Block", t, Vector3.zero, Vector3.one * 0.07f, mats["Mat_White"], new Vector3(35f, 45f, 0f));
                    Prim(PrimitiveType.Cube, "Core", t, Vector3.zero, Vector3.one * 0.045f, mats["Mat_Turquoise"]);
                },
                2f, 0.25f, 1.6f, 1f, 0f);

            var defs = new[]
            {
                new TowerDef
                {
                    type = TowerType.Bass, name = "Bass", description = "Onda potente\ncadencia lenta", cost = 50, halfBeats = 4,
                    hair = Turquoise, dress = Ink, accent = Turquoise, projectile = bassProj,
                    stats = new[]
                    {
                        new TowerStats { damage = 34f, range = 0.50f, upgradeCost = 60 },
                        new TowerStats { damage = 52f, range = 0.58f, upgradeCost = 90 },
                        new TowerStats { damage = 80f, range = 0.68f, upgradeCost = 0 }
                    }
                },
                new TowerDef
                {
                    type = TowerType.Treble, name = "Treble", description = "Notas rápidas\ndaño bajo", cost = 40, halfBeats = 1,
                    hair = Magenta, dress = White, accent = Magenta, projectile = trebleProj,
                    stats = new[]
                    {
                        new TowerStats { damage = 7f, range = 0.42f, upgradeCost = 50 },
                        new TowerStats { damage = 11f, range = 0.50f, upgradeCost = 80 },
                        new TowerStats { damage = 16f, range = 0.58f, upgradeCost = 0 }
                    }
                },
                new TowerDef
                {
                    type = TowerType.Echo, name = "Echo", description = "Pulso en área\nralentiza", cost = 60, halfBeats = 4,
                    hair = IceCyan, dress = White, accent = IceCyan, projectile = echoProj,
                    stats = new[]
                    {
                        new TowerStats { damage = 12f, range = 0.45f, upgradeCost = 70 },
                        new TowerStats { damage = 20f, range = 0.52f, upgradeCost = 100 },
                        new TowerStats { damage = 30f, range = 0.60f, upgradeCost = 0 }
                    }
                },
                new TowerDef
                {
                    type = TowerType.Drop, name = "Drop", description = "Explosión\nque empuja", cost = 80, halfBeats = 6,
                    hair = White, dress = Turquoise, accent = White, projectile = dropProj,
                    stats = new[]
                    {
                        new TowerStats { damage = 45f, range = 0.42f, upgradeCost = 90 },
                        new TowerStats { damage = 70f, range = 0.50f, upgradeCost = 130 },
                        new TowerStats { damage = 105f, range = 0.58f, upgradeCost = 0 }
                    }
                }
            };

            for (int i = 0; i < defs.Length; i++)
            {
                defs[i].prefab = CreateTowerPrefab(defs[i]);
            }
            return defs;
        }

        private static Material ColorMat(Color color)
        {
            if (color == Turquoise) return mats["Mat_Turquoise"];
            if (color == Magenta) return mats["Mat_Magenta"];
            if (color == IceCyan) return mats["Mat_IceCyan"];
            if (color == White) return mats["Mat_White"];
            return mats["Mat_Ink"];
        }

        private static GameObject CreateTowerPrefab(TowerDef def)
        {
            var root = new GameObject($"Tower_{def.name}") { tag = "Tower" };
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.18f, 0.3f, 0.18f);
            col.center = new Vector3(0f, 0.15f, 0f);

            // Altavoz que sirve de pedestal
            Prim(PrimitiveType.Cube, "Speaker", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.16f, 0.08f, 0.16f), mats["Mat_Ink"]);
            Prim(PrimitiveType.Cylinder, "SpeakerCone", root.transform, new Vector3(0f, 0.04f, 0.081f), new Vector3(0.09f, 0.003f, 0.09f), ColorMat(def.accent), new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Trim", root.transform, new Vector3(0f, 0.082f, 0f), new Vector3(0.17f, 0.006f, 0.17f), ColorMat(def.accent));

            // Cantante: es la parte que gira hacia el objetivo
            Transform singer = Empty("Singer", root.transform, new Vector3(0f, 0.085f, 0f)).transform;
            BuildChibi(singer, "Chibi", ColorMat(def.hair), ColorMat(def.dress), ColorMat(def.accent), 0.17f);
            Transform firePoint = Empty("FirePoint", singer, new Vector3(0f, 0.1f, 0.07f)).transform;

            GameObject range = Prim(PrimitiveType.Cylinder, "RangeVisualizer", root.transform, new Vector3(0f, 0.006f, 0f), new Vector3(1f, 0.0015f, 1f), mats["Mat_Range"]);
            range.SetActive(false);

            // Aviso de "silenciada": una X magenta sobre la cantante
            GameObject silence = Empty("SilenceIndicator", root.transform, new Vector3(0f, 0.32f, 0f));
            Prim(PrimitiveType.Cube, "Bar1", silence.transform, Vector3.zero, new Vector3(0.1f, 0.018f, 0.018f), mats["Mat_Magenta"], new Vector3(0f, 0f, 45f));
            Prim(PrimitiveType.Cube, "Bar2", silence.transform, Vector3.zero, new Vector3(0.1f, 0.018f, 0.018f), mats["Mat_Magenta"], new Vector3(0f, 0f, -45f));
            silence.SetActive(false);

            GameObject lightObj = Empty("NightLight", root.transform, new Vector3(0f, 0.22f, 0f));
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = def.accent == White ? Turquoise : def.accent;
            light.range = 0.5f;
            light.intensity = 1.2f;
            light.enabled = false;

            Tower tower = root.AddComponent<Tower>();
            SetProps(tower,
                ("towerType", def.type), ("towerName", def.name), ("baseCost", def.cost), ("halfBeatsPerShot", def.halfBeats),
                ("projectilePrefab", def.projectile), ("firePoint", firePoint), ("rotatorPart", singer),
                ("shotVfxPrefab", vfx["Vfx_Shot"]), ("rangeVisualizer", range), ("silenceIndicator", silence), ("nightLight", light));

            var so = new SerializedObject(tower);
            SerializedProperty stats = so.FindProperty("statsPerLevel");
            stats.arraySize = def.stats.Length;
            for (int i = 0; i < def.stats.Length; i++)
            {
                SerializedProperty element = stats.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("damage").floatValue = def.stats[i].damage;
                element.FindPropertyRelative("range").floatValue = def.stats[i].range;
                element.FindPropertyRelative("upgradeCost").intValue = def.stats[i].upgradeCost;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, $"{PrefabsPath}/Towers/Tower_{def.name}.prefab");
        }

        #endregion

        #region Enemigos y jefes

        private static GameObject NewEnemyRoot(string name, Vector3 colliderSize)
        {
            var root = new GameObject(name) { tag = "Enemy" };
            var col = root.AddComponent<BoxCollider>();
            col.size = colliderSize;

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearDamping = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            return root;
        }

        private static void SetEnemyProps(Enemy enemy, string glitchName, float health, float speed, int coins, int stageDamage,
            float hover, float knockbackResistance, GameObject healthBar, float barHeight)
        {
            SetProps(enemy,
                ("glitchName", glitchName), ("maxHealth", health), ("moveSpeed", speed), ("coinsReward", coins),
                ("stageDamage", stageDamage), ("hoverHeight", hover), ("knockbackResistance", knockbackResistance),
                ("deathVfxPrefab", vfx["Vfx_Death"]), ("healthBarPrefab", healthBar), ("healthBarHeight", barHeight));
        }

        private static Dictionary<string, GameObject> CreateEnemies(GameObject enemyBar, GameObject bossBar)
        {
            var dict = new Dictionary<string, GameObject>();
            Material red = mats["Mat_GlitchRed"];
            Material black = mats["Mat_GlitchBlack"];

            // Zona de ruido del jefe Distorsión
            {
                var root = new GameObject("NoiseZone");
                var col = root.AddComponent<SphereCollider>();
                col.radius = 0.2f;
                col.isTrigger = true;
                Prim(PrimitiveType.Cylinder, "Disc", root.transform, Vector3.zero, new Vector3(0.4f, 0.002f, 0.4f), mats["Mat_Noise"]);
                Prim(PrimitiveType.Cylinder, "Core", root.transform, new Vector3(0f, 0.002f, 0f), new Vector3(0.18f, 0.002f, 0.18f), mats["Mat_Noise"]);
                root.AddComponent<NoiseZone>();
                dict["NoiseZone"] = SavePrefab(root, $"{PrefabsPath}/Enemies/NoiseZone.prefab");
            }

            // Pixel: básico (vida baja, velocidad media)
            {
                GameObject root = NewEnemyRoot("Enemy_Pixel", Vector3.one * 0.1f);
                Prim(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, Vector3.one * 0.085f, red);
                Prim(PrimitiveType.Cube, "Shard1", root.transform, new Vector3(0.04f, 0.04f, 0.02f), Vector3.one * 0.04f, black);
                Prim(PrimitiveType.Cube, "Shard2", root.transform, new Vector3(-0.045f, -0.02f, -0.03f), Vector3.one * 0.035f, black);
                SetEnemyProps(root.AddComponent<Enemy>(), "Pixel", 40f, 0.22f, 8, 1, 0.06f, 0f, enemyBar, 0.1f);
                dict["Enemy_Pixel"] = SavePrefab(root, $"{PrefabsPath}/Enemies/Enemy_Pixel.prefab");
            }

            // Static: rápido (vida muy baja, velocidad alta)
            {
                GameObject root = NewEnemyRoot("Enemy_Static", Vector3.one * 0.09f);
                Prim(PrimitiveType.Cube, "Bolt1", root.transform, Vector3.zero, new Vector3(0.13f, 0.02f, 0.02f), red, new Vector3(0f, 0f, 35f));
                Prim(PrimitiveType.Cube, "Bolt2", root.transform, Vector3.zero, new Vector3(0.13f, 0.02f, 0.02f), black, new Vector3(0f, 60f, -35f));
                Prim(PrimitiveType.Cube, "Bolt3", root.transform, Vector3.zero, new Vector3(0.02f, 0.13f, 0.02f), red, new Vector3(20f, 0f, 0f));
                SetEnemyProps(root.AddComponent<Enemy>(), "Static", 22f, 0.4f, 6, 1, 0.07f, 0f, enemyBar, 0.11f);
                dict["Enemy_Static"] = SavePrefab(root, $"{PrefabsPath}/Enemies/Enemy_Static.prefab");
            }

            // Amp: tanque (vida alta, velocidad baja)
            {
                GameObject root = NewEnemyRoot("Enemy_Amp", new Vector3(0.15f, 0.14f, 0.12f));
                Prim(PrimitiveType.Cube, "Cabinet", root.transform, Vector3.zero, new Vector3(0.15f, 0.14f, 0.11f), black);
                Prim(PrimitiveType.Cylinder, "Cone", root.transform, new Vector3(0f, 0f, 0.056f), new Vector3(0.1f, 0.003f, 0.1f), red, new Vector3(90f, 0f, 0f));
                Prim(PrimitiveType.Cube, "Knobs", root.transform, new Vector3(0f, 0.075f, 0f), new Vector3(0.12f, 0.012f, 0.03f), red);
                SetEnemyProps(root.AddComponent<Enemy>(), "Amp", 160f, 0.13f, 20, 1, 0.075f, 0.5f, enemyBar, 0.13f);
                dict["Enemy_Amp"] = SavePrefab(root, $"{PrefabsPath}/Enemies/Enemy_Amp.prefab");
            }

            // Jefe 1: Distorsión
            {
                GameObject root = NewEnemyRoot("Boss_Distortion", Vector3.one * 0.24f);
                Prim(PrimitiveType.Cube, "Core", root.transform, Vector3.zero, Vector3.one * 0.2f, black, new Vector3(0f, 45f, 0f));
                Prim(PrimitiveType.Cube, "Slice1", root.transform, new Vector3(0.03f, 0.05f, 0f), new Vector3(0.26f, 0.03f, 0.22f), red);
                Prim(PrimitiveType.Cube, "Slice2", root.transform, new Vector3(-0.03f, -0.04f, 0f), new Vector3(0.26f, 0.03f, 0.22f), red);
                BossDistortion boss = root.AddComponent<BossDistortion>();
                SetEnemyProps(boss, "Distorsión", 700f, 0.085f, 100, 5, 0.13f, 0.85f, bossBar, 0.2f);
                SetProps(boss, ("bossType", BossType.Distortion), ("bossTitle", "Distorsión"), ("abilityInterval", 5f),
                    ("abilityVfxPrefab", vfx["Vfx_MagentaPulse"]), ("noiseZonePrefab", dict["NoiseZone"]));
                dict["Boss_Distortion"] = SavePrefab(root, $"{PrefabsPath}/Bosses/Boss_Distortion.prefab");
            }

            // Jefe 2: Feedback
            {
                GameObject root = NewEnemyRoot("Boss_Feedback", Vector3.one * 0.24f);
                Prim(PrimitiveType.Sphere, "Core", root.transform, Vector3.zero, Vector3.one * 0.2f, red);
                Prim(PrimitiveType.Cylinder, "Ring1", root.transform, Vector3.zero, new Vector3(0.3f, 0.006f, 0.3f), black);
                Prim(PrimitiveType.Cylinder, "Ring2", root.transform, Vector3.zero, new Vector3(0.3f, 0.006f, 0.3f), black, new Vector3(0f, 0f, 90f));
                BossFeedback boss = root.AddComponent<BossFeedback>();
                SetEnemyProps(boss, "Feedback", 1000f, 0.1f, 140, 5, 0.15f, 0.85f, bossBar, 0.22f);
                SetProps(boss, ("bossType", BossType.Feedback), ("bossTitle", "Feedback"), ("abilityInterval", 7f),
                    ("abilityVfxPrefab", vfx["Vfx_MagentaPulse"]), ("silenceRadius", 0.6f), ("silenceDuration", 12f));
                dict["Boss_Feedback"] = SavePrefab(root, $"{PrefabsPath}/Bosses/Boss_Feedback.prefab");
            }

            // Jefe 3: Reina Glitch
            {
                GameObject root = NewEnemyRoot("Boss_GlitchQueen", new Vector3(0.22f, 0.3f, 0.22f));
                Prim(PrimitiveType.Cylinder, "Gown", root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(0.22f, 0.09f, 0.22f), black);
                Prim(PrimitiveType.Cube, "Bust", root.transform, new Vector3(0f, 0.07f, 0f), Vector3.one * 0.12f, red, new Vector3(0f, 45f, 0f));
                for (int i = 0; i < 5; i++)
                {
                    float angle = i / 5f * Mathf.PI * 2f;
                    Prim(PrimitiveType.Cube, $"Crown{i}", root.transform, new Vector3(Mathf.Cos(angle) * 0.06f, 0.16f, Mathf.Sin(angle) * 0.06f), new Vector3(0.02f, 0.06f, 0.02f), red);
                }
                BossGlitchQueen boss = root.AddComponent<BossGlitchQueen>();
                SetEnemyProps(boss, "Reina Glitch", 1300f, 0.095f, 180, 5, 0.15f, 0.85f, bossBar, 0.26f);
                SetProps(boss, ("bossType", BossType.GlitchQueen), ("bossTitle", "Reina Glitch"), ("abilityInterval", 8f),
                    ("abilityVfxPrefab", vfx["Vfx_Death"]), ("minionPrefab", dict["Enemy_Pixel"]), ("minionsPerThreshold", 2), ("minionsOnDeath", 3));
                dict["Boss_GlitchQueen"] = SavePrefab(root, $"{PrefabsPath}/Bosses/Boss_GlitchQueen.prefab");
            }

            // Jefe 4: Mezcla Final
            {
                GameObject root = NewEnemyRoot("Boss_FinalMix", new Vector3(0.28f, 0.34f, 0.28f));
                Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.28f, 0.14f, 0.24f), black);
                Prim(PrimitiveType.Sphere, "Core", root.transform, new Vector3(0f, 0.06f, 0f), Vector3.one * 0.18f, red);
                Prim(PrimitiveType.Cylinder, "Halo", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.32f, 0.006f, 0.32f), black, new Vector3(20f, 0f, 0f));
                Prim(PrimitiveType.Cube, "Fader1", root.transform, new Vector3(-0.08f, 0f, 0.125f), new Vector3(0.03f, 0.08f, 0.01f), red);
                Prim(PrimitiveType.Cube, "Fader2", root.transform, new Vector3(0f, -0.02f, 0.125f), new Vector3(0.03f, 0.08f, 0.01f), red);
                Prim(PrimitiveType.Cube, "Fader3", root.transform, new Vector3(0.08f, -0.04f, 0.125f), new Vector3(0.03f, 0.08f, 0.01f), red);
                BossFinalMix boss = root.AddComponent<BossFinalMix>();
                SetEnemyProps(boss, "Mezcla Final", 2200f, 0.08f, 250, 5, 0.17f, 0.9f, bossBar, 0.28f);
                SetProps(boss, ("bossType", BossType.FinalMix), ("bossTitle", "Mezcla Final"), ("abilityInterval", 6f),
                    ("abilityVfxPrefab", vfx["Vfx_MagentaPulse"]), ("noiseZonePrefab", dict["NoiseZone"]), ("minionPrefab", dict["Enemy_Pixel"]));
                dict["Boss_FinalMix"] = SavePrefab(root, $"{PrefabsPath}/Bosses/Boss_FinalMix.prefab");
            }

            return dict;
        }

        #endregion

        #region Battlefield

        private static readonly Vector3[] PathPoints =
        {
            new Vector3(-0.6f, 0f, -0.78f),
            new Vector3(-0.6f, 0f, -0.2f),
            new Vector3(-0.1f, 0f, -0.2f),
            new Vector3(-0.1f, 0f, 0.35f),
            new Vector3(0.35f, 0f, 0.35f),
            new Vector3(0.35f, 0f, -0.1f),
            new Vector3(0.65f, 0f, -0.1f),
            new Vector3(0.65f, 0f, 0.5f)
        };

        private static GameObject CreateBattlefieldPrefab(GameObject avatarPrefab)
        {
            var root = new GameObject("Battlefield");
            Transform rt = root.transform;

            // --- Suelo holográfico con cuadrícula luminosa (GDD 8) ---
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.tag = "Field";
            ground.transform.SetParent(rt, false);
            ground.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(1.7f, 0.04f, 1.7f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_Ground"];

            Transform grid = Empty("Grid", rt, Vector3.zero).transform;
            for (int i = -4; i <= 4; i++)
            {
                float offset = i * 0.2f;
                bool edge = Mathf.Abs(i) == 4;
                float thickness = edge ? 0.012f : 0.005f;
                Prim(PrimitiveType.Cube, $"LineX{i}", grid, new Vector3(0f, 0.0015f, offset), new Vector3(1.62f, 0.002f, thickness), mats["Mat_Grid"]);
                Prim(PrimitiveType.Cube, $"LineZ{i}", grid, new Vector3(offset, 0.0015f, 0f), new Vector3(thickness, 0.002f, 1.62f), mats["Mat_Grid"]);
            }

            // --- Camino de los glitches ---
            Transform path = Empty("Path", rt, Vector3.zero).transform;
            Transform pathVisual = Empty("PathVisual", rt, Vector3.zero).transform;
            for (int i = 0; i < PathPoints.Length; i++)
            {
                Empty($"Waypoint_{i}", path, PathPoints[i]);
                Prim(PrimitiveType.Cylinder, $"Corner_{i}", pathVisual, PathPoints[i] + Vector3.up * 0.003f, new Vector3(0.13f, 0.002f, 0.13f), mats["Mat_Path"]);
                if (i > 0)
                {
                    Segment($"Strip_{i}", pathVisual, PathPoints[i - 1] + Vector3.up * 0.003f, PathPoints[i] + Vector3.up * 0.003f, 0.13f, 0.004f, mats["Mat_Path"]);
                }
            }
            Prim(PrimitiveType.Cube, "Portal", pathVisual, PathPoints[0] + new Vector3(0f, 0.09f, -0.03f), new Vector3(0.18f, 0.18f, 0.02f), mats["Mat_GlitchRed"]);
            Prim(PrimitiveType.Cube, "PortalCore", pathVisual, PathPoints[0] + new Vector3(0f, 0.09f, -0.018f), new Vector3(0.13f, 0.13f, 0.02f), mats["Mat_GlitchBlack"]);

            // --- Escenario a defender ---
            Vector3 stagePos = new Vector3(0.65f, 0f, 0.64f);
            Transform stage = Empty("Stage", rt, stagePos).transform;
            Prim(PrimitiveType.Cylinder, "Platform", stage, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.02f, 0.3f), mats["Mat_Turquoise"]);
            Prim(PrimitiveType.Cylinder, "PlatformTop", stage, new Vector3(0f, 0.042f, 0f), new Vector3(0.26f, 0.002f, 0.26f), mats["Mat_Ink"]);
            Prim(PrimitiveType.Cube, "Screen", stage, new Vector3(0f, 0.2f, 0.13f), new Vector3(0.3f, 0.2f, 0.012f), mats["Mat_Hologram"]);
            Prim(PrimitiveType.Cube, "ScreenFrame", stage, new Vector3(0f, 0.2f, 0.138f), new Vector3(0.32f, 0.22f, 0.006f), mats["Mat_Ink"]);
            Prim(PrimitiveType.Cube, "Speaker_L", stage, new Vector3(-0.17f, 0.07f, 0.08f), new Vector3(0.06f, 0.14f, 0.06f), mats["Mat_Ink"]);
            Prim(PrimitiveType.Cube, "Speaker_R", stage, new Vector3(0.17f, 0.07f, 0.08f), new Vector3(0.06f, 0.14f, 0.06f), mats["Mat_Ink"]);
            Transform idol = BuildChibi(stage, "Idol", mats["Mat_Turquoise"], mats["Mat_White"], mats["Mat_Magenta"], 0.15f);
            idol.localPosition = new Vector3(0f, 0.044f, 0f);
            idol.localEulerAngles = new Vector3(0f, 180f, 0f);

            // --- Plataformas de construcción ---
            Transform spots = Empty("BuildSpots", rt, Vector3.zero).transform;
            Vector3[] spotPositions =
            {
                new Vector3(-0.35f, 0f, -0.48f),
                new Vector3(-0.38f, 0f, 0.08f),
                new Vector3(0.12f, 0f, 0.08f),
                new Vector3(0.12f, 0f, 0.6f),
                new Vector3(0.5f, 0f, 0.14f),
                new Vector3(0.6f, 0f, -0.4f)
            };

            for (int i = 0; i < spotPositions.Length; i++)
            {
                GameObject spot = Empty($"BuildSpot_{i + 1}", spots, spotPositions[i]);
                spot.tag = "BuildSpot";
                var col = spot.AddComponent<BoxCollider>();
                col.size = new Vector3(0.2f, 0.05f, 0.2f);
                col.center = new Vector3(0f, 0.025f, 0f);

                Prim(PrimitiveType.Cylinder, "Base", spot.transform, new Vector3(0f, 0.005f, 0f), new Vector3(0.2f, 0.005f, 0.2f), mats["Mat_Ink"]);
                GameObject indicator = Prim(PrimitiveType.Cylinder, "Indicator", spot.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.16f, 0.002f, 0.16f), mats["Mat_Indicator"]);

                BuildSpot buildSpot = spot.AddComponent<BuildSpot>();
                SetProps(buildSpot, ("requireAvatarNearby", true), ("avatarProximityDistance", 0.45f), ("availableIndicator", indicator.GetComponent<MeshRenderer>()));
            }

            // --- Pads de teletransporte (dos parejas) ---
            Transform pads = Empty("TeleportPads", rt, Vector3.zero).transform;
            Vector3[] padPositions =
            {
                new Vector3(-0.62f, 0f, 0.6f),
                new Vector3(0.1f, 0f, -0.6f),
                new Vector3(0.42f, 0f, 0.66f),
                new Vector3(-0.3f, 0f, 0.45f)
            };

            var padComponents = new TeleportPad[padPositions.Length];
            for (int i = 0; i < padPositions.Length; i++)
            {
                GameObject pad = Empty($"TeleportPad_{(char)('A' + i)}", pads, padPositions[i]);
                pad.tag = "TeleportPad";
                var col = pad.AddComponent<BoxCollider>();
                col.size = new Vector3(0.18f, 0.05f, 0.18f);
                col.center = new Vector3(0f, 0.025f, 0f);

                Prim(PrimitiveType.Cylinder, "Base", pad.transform, new Vector3(0f, 0.005f, 0f), new Vector3(0.18f, 0.005f, 0.18f), mats["Mat_White"]);
                Prim(PrimitiveType.Cylinder, "Glow", pad.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.13f, 0.002f, 0.13f), mats["Mat_Hologram"]);
                Prim(PrimitiveType.Cylinder, "Center", pad.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.05f, 0.002f, 0.05f), mats["Mat_Magenta"]);

                padComponents[i] = pad.AddComponent<TeleportPad>();
            }
            SetProps(padComponents[0], ("pairedPad", padComponents[1]));
            SetProps(padComponents[1], ("pairedPad", padComponents[0]));
            SetProps(padComponents[2], ("pairedPad", padComponents[3]));
            SetProps(padComponents[3], ("pairedPad", padComponents[2]));

            // --- Montaña rusa: baja de una estación elevada, recorre el camino y vuelve a subir ---
            Transform coaster = Empty("RollerCoaster", rt, Vector3.zero).transform;
            Transform track = Empty("Track", coaster, Vector3.zero).transform;
            Transform rails = Empty("Rails", coaster, Vector3.zero).transform;

            const float rideHeight = 0.06f;
            var trackPoints = new List<Vector3> { new Vector3(-0.84f, 0.42f, -0.84f), new Vector3(-0.6f, rideHeight, -0.7f) };
            for (int i = 1; i < PathPoints.Length - 1; i++)
            {
                trackPoints.Add(PathPoints[i] + Vector3.up * rideHeight);
            }
            trackPoints.Add(new Vector3(0.65f, rideHeight, 0.3f));
            trackPoints.Add(new Vector3(0.84f, 0.42f, -0.1f));

            for (int i = 0; i < trackPoints.Count; i++)
            {
                Empty($"TrackWP_{i}", track, trackPoints[i]);
                if (i == 0) continue;

                Vector3 a = trackPoints[i - 1] - Vector3.up * 0.045f;
                Vector3 b = trackPoints[i] - Vector3.up * 0.045f;
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized).normalized * 0.03f;
                Segment($"RailL_{i}", rails, a - side, b - side, 0.008f, 0.008f, mats["Mat_Magenta"]);
                Segment($"RailR_{i}", rails, a + side, b + side, 0.008f, 0.008f, mats["Mat_Magenta"]);
            }

            // Soportes de los tramos elevados
            foreach (Vector3 top in new[] { trackPoints[0], Vector3.Lerp(trackPoints[0], trackPoints[1], 0.5f), trackPoints[trackPoints.Count - 1], Vector3.Lerp(trackPoints[trackPoints.Count - 1], trackPoints[trackPoints.Count - 2], 0.5f) })
            {
                Vector3 railPoint = top - Vector3.up * 0.045f;
                Segment("Support", rails, new Vector3(railPoint.x, 0f, railPoint.z), railPoint, 0.012f, 0.012f, mats["Mat_White"]);
            }

            var cart = new GameObject("Cart");
            cart.transform.SetParent(coaster, false);
            cart.transform.localPosition = trackPoints[0];
            var cartCol = cart.AddComponent<BoxCollider>();
            cartCol.size = new Vector3(0.14f, 0.1f, 0.2f);
            cartCol.isTrigger = true;
            var cartRb = cart.AddComponent<Rigidbody>();
            cartRb.useGravity = false;
            cartRb.isKinematic = true;
            Prim(PrimitiveType.Cube, "Body", cart.transform, Vector3.zero, new Vector3(0.12f, 0.06f, 0.18f), mats["Mat_Turquoise"]);
            Prim(PrimitiveType.Cube, "Nose", cart.transform, new Vector3(0f, 0f, 0.1f), new Vector3(0.1f, 0.05f, 0.05f), mats["Mat_Magenta"], new Vector3(45f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Seat", cart.transform, new Vector3(0f, 0.04f, -0.04f), new Vector3(0.1f, 0.05f, 0.03f), mats["Mat_Ink"]);
            cart.AddComponent<RollerCoasterCart>();
            cart.SetActive(false);

            // --- Avatar y contenedores ---
            GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab, rt);
            avatar.name = "Avatar";
            avatar.transform.localPosition = new Vector3(0.12f, 0f, -0.28f);

            Transform towers = Empty("Towers", rt, Vector3.zero).transform;
            Transform enemies = Empty("Enemies", rt, Vector3.zero).transform;
            Transform projectiles = Empty("Projectiles", rt, Vector3.zero).transform;

            Battlefield battlefield = root.AddComponent<Battlefield>();
            SetProps(battlefield,
                ("stage", stage), ("path", path), ("rollerCoasterTrack", track), ("rollerCoasterCart", cart),
                ("avatar", avatar.transform), ("towers", towers), ("enemies", enemies), ("projectiles", projectiles));

            // Tamaño por defecto: ~1 m de lado, cómodo para una mesa (el jugador puede escalarlo)
            rt.localScale = Vector3.one * 0.6f;

            return SavePrefab(root, $"{PrefabsPath}/Environment/Battlefield.prefab");
        }

        #endregion

        #region Escena

        private static void AssembleScene(AudioClip bgm, Dictionary<SfxId, AudioClip> sfxClips, GameObject battlefieldPrefab,
            GameObject reticlePrefab, TowerDef[] towers, Dictionary<string, GameObject> enemies)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Quitar los restos del avance anterior (campo guardado en escena, menús duplicados, spawner de la plantilla)
            string[] purge = { "Battlefield(Clone)", "Battlefield", "Object Spawner", "ConcertDefense_HUD_Canvas", "WorldSpace_TowerMenu", "PlacementReticle", "[GameManagers]", "ARPlacementManager" };
            var toRemove = new List<GameObject>();
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (purge.Contains(t.name)) toRemove.Add(t.gameObject);
                }
            }
            foreach (GameObject go in toRemove)
            {
                if (go == null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) go.SetActive(false);
                else UnityEngine.Object.DestroyImmediate(go);
            }

            // 2. AR Session y XR Origin (de la plantilla AR Mobile)
            if (FindInScene<ARSession>(scene) == null)
            {
                new GameObject("AR Session").AddComponent<ARSession>();
            }

            XROrigin origin = FindInScene<XROrigin>(scene);
            if (origin == null)
            {
                Debug.LogError("[ConcertDefense] La escena no tiene XR Origin. Añádelo con GameObject > XR > XR Origin (Mobile AR) y vuelve a ejecutar el montaje.");
                return;
            }

            GameObject originObj = origin.gameObject;
            ARRaycastManager raycastManager = GetOrAdd<ARRaycastManager>(originObj);
            ARPlaneManager planeManager = GetOrAdd<ARPlaneManager>(originObj);

            Camera cam = originObj.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.gameObject.tag = "MainCamera";
                GetOrAdd<AudioListener>(cam.gameObject);
                cam.nearClipPlane = 0.05f;
            }
            ARCameraManager cameraManager = originObj.GetComponentInChildren<ARCameraManager>(true);

            // 3. Retícula
            GameObject reticleObj = (GameObject)PrefabUtility.InstantiatePrefab(reticlePrefab);
            reticleObj.name = "PlacementReticle";

            ARPlacementController placer = GetOrAdd<ARPlacementController>(originObj);
            SetProps(placer,
                ("raycastManager", raycastManager), ("planeManager", planeManager),
                ("placementReticle", reticleObj.GetComponent<PlacementReticle>()), ("battlefieldPrefab", battlefieldPrefab));

            FieldManipulator manipulator = GetOrAdd<FieldManipulator>(originObj);
            SetProps(manipulator, ("raycastManager", raycastManager));

            // El controlador de luz va en la Directional Light (GDD 11)
            LightEstimationController oldLightController = originObj.GetComponent<LightEstimationController>();
            if (oldLightController != null) UnityEngine.Object.DestroyImmediate(oldLightController);

            Light directional = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional);
            if (directional == null)
            {
                var lightObj = new GameObject("Directional Light");
                directional = lightObj.AddComponent<Light>();
                directional.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            LightEstimationController lightController = GetOrAdd<LightEstimationController>(directional.gameObject);
            SetProps(lightController, ("cameraManager", cameraManager), ("directionalLight", directional));

            // 4. EventSystem con el módulo del Input System
            EventSystem eventSystem = FindInScene<EventSystem>(scene);
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }
            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) UnityEngine.Object.DestroyImmediate(legacyModule);
            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            // 5. Sistemas del juego
            var managers = new GameObject("[GameManagers]");
            managers.AddComponent<GameManager>();
            ConfigureWaves(managers.AddComponent<WaveSpawner>(), enemies);

            AudioSource music = managers.AddComponent<AudioSource>();
            music.clip = bgm;
            music.loop = true;
            music.playOnAwake = false;
            music.volume = 0.55f;
            BeatClock beatClock = managers.AddComponent<BeatClock>();
            SetProps(beatClock, ("bpm", 120f), ("musicSource", music), ("autoStartOnPlaying", true));

            UltimateController ultimate = managers.AddComponent<UltimateController>();
            managers.AddComponent<TouchInputRouter>();

            GameObject sfxObj = Empty("SfxSource", managers.transform, Vector3.zero);
            AudioSource sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            Sfx sfx = sfxObj.AddComponent<Sfx>();
            var sfxSo = new SerializedObject(sfx);
            sfxSo.FindProperty("source").objectReferenceValue = sfxSource;
            SerializedProperty entries = sfxSo.FindProperty("entries");
            entries.arraySize = sfxClips.Count;
            int entryIndex = 0;
            foreach (KeyValuePair<SfxId, AudioClip> pair in sfxClips)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex++);
                entry.FindPropertyRelative("id").enumValueIndex = (int)pair.Key;
                entry.FindPropertyRelative("clip").objectReferenceValue = pair.Value;
                entry.FindPropertyRelative("volume").floatValue = pair.Key == SfxId.Shoot ? 0.35f : 0.8f;
            }
            sfxSo.ApplyModifiedPropertiesWithoutUndo();

            // 6. Interfaz
            BuildHud(towers, ultimate);
            BuildWorldSpaceTowerMenu();

            Debug.Log("[ConcertDefense] Objetos raíz de la escena: " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name)));
            Debug.Log("[ConcertDefense] XR Origin: " + string.Join(", ", originObj.GetComponents<Component>().Select(c => c.GetType().Name))
                + " | EventSystem: " + string.Join(", ", eventSystem.GetComponents<Component>().Select(c => c.GetType().Name)));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            }
        }

        /// <summary>Busca un componente en la escena, incluidos objetos inactivos.</summary>
        private static T FindInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                T found = sceneRoot.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        /// <summary>
        /// Oleadas del GDD 6.
        /// </summary>
        private static void ConfigureWaves(WaveSpawner spawner, Dictionary<string, GameObject> enemies)
        {
            var so = new SerializedObject(spawner);
            SerializedProperty waves = so.FindProperty("waves");
            waves.arraySize = 4;

            void SetWave(int index, string title, string boss, params (string key, int count, float interval)[] groups)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("waveTitle").stringValue = title;
                wave.FindPropertyRelative("delayBeforeBoss").floatValue = 4f;
                wave.FindPropertyRelative("bossPrefab").objectReferenceValue = enemies[boss];

                SerializedProperty groupsProp = wave.FindPropertyRelative("enemyGroups");
                groupsProp.arraySize = groups.Length;
                for (int g = 0; g < groups.Length; g++)
                {
                    SerializedProperty group = groupsProp.GetArrayElementAtIndex(g);
                    group.FindPropertyRelative("groupName").stringValue = $"{groups[g].count}x {groups[g].key}";
                    group.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemies[groups[g].key];
                    group.FindPropertyRelative("count").intValue = groups[g].count;
                    group.FindPropertyRelative("spawnInterval").floatValue = groups[g].interval;
                }
            }

            SetWave(0, "Ruido blanco", "Boss_Distortion", ("Enemy_Pixel", 8, 1.6f));
            SetWave(1, "Acople", "Boss_Feedback", ("Enemy_Pixel", 10, 1.4f), ("Enemy_Static", 5, 0.9f));
            SetWave(2, "Enjambre", "Boss_GlitchQueen", ("Enemy_Pixel", 8, 1.2f), ("Enemy_Static", 8, 0.8f), ("Enemy_Amp", 3, 2.5f));
            SetWave(3, "Mezcla final", "Boss_FinalMix", ("Enemy_Pixel", 10, 1.1f), ("Enemy_Static", 10, 0.7f), ("Enemy_Amp", 5, 2.2f));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion

        #region Interfaz

        private static RectTransform UIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static Image UIImage(RectTransform rect, Color color, Sprite sprite, bool raycastTarget)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite != null && sprite == roundedSprite) image.type = Image.Type.Sliced;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TextMeshProUGUI UIText(RectTransform rect, string content, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Texto que ocupa todo el padre, con un margen.</summary>
        private static TextMeshProUGUI UILabel(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions alignment, float padding = 6f)
        {
            RectTransform rect = UIRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-padding * 2f, -padding * 2f));
            return UIText(rect, content, size, color, alignment);
        }

        private static Button UIButton(RectTransform rect, Color color, Sprite sprite, string label, float fontSize, Color labelColor, out TextMeshProUGUI labelText)
        {
            Image image = UIImage(rect, color, sprite, true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f);
            button.colors = colors;

            labelText = UILabel("Label", rect, label, fontSize, labelColor, TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>Barra con relleno anclado (sin Slider): devuelve el RectTransform del relleno.</summary>
        private static RectTransform UIBar(RectTransform rect, Color background, Color fillColor)
        {
            UIImage(rect, background, roundedSprite, false);
            RectTransform area = UIRect("FillArea", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, -6f));
            RectTransform fill = UIRect("Fill", area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIImage(fill, fillColor, null, false);
            return fill;
        }

        private static void BuildHud(TowerDef[] towers, UltimateController ultimate)
        {
            var center = new Vector2(0.5f, 0.5f);

            var canvasObj = new GameObject("ConcertDefense_HUD_Canvas", typeof(RectTransform)) { layer = 5 };
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            Transform root = canvasObj.transform;

            // ---------- HUD de juego: siempre visible ----------
            RectTransform gameplay = UIRect("GameplayHUD_Root", root, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);

            RectTransform topBar = UIRect("TopBar", gameplay, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-28f, 60f));
            UIImage(topBar, PanelColor, roundedSprite, false);

            RectTransform coinsRT = UIRect("CoinsText", topBar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(300f, 0f));
            TextMeshProUGUI coinsText = UIText(coinsRT, "MONEDAS  <b>150</b>", 26f, new Color(1f, 0.88f, 0.25f), TextAlignmentOptions.MidlineLeft);

            RectTransform healthBar = UIRect("StageHealthBar", topBar, center, center, center, Vector2.zero, new Vector2(360f, 36f));
            RectTransform healthFill = UIBar(healthBar, new Color(0.12f, 0.14f, 0.2f, 1f), Turquoise);
            TextMeshProUGUI healthText = UILabel("HealthText", healthBar, "ÁNIMO  20 / 20", 20f, Color.white, TextAlignmentOptions.Center, 2f);

            RectTransform waveRT = UIRect("WaveText", topBar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(300f, 0f));
            TextMeshProUGUI waveText = UIText(waveRT, "OLEADA  - / 4", 26f, Turquoise, TextAlignmentOptions.MidlineRight);

            // Botón Iniciar oleada
            RectTransform startRT = UIRect("StartWaveButton", gameplay, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(340f, 78f));
            Button startButton = UIButton(startRT, Turquoise, roundedSprite, "INICIAR OLEADA", 28f, Ink, out TextMeshProUGUI startLabel);

            // Botón Reubicar
            RectTransform relocateRT = UIRect("RelocateButton", gameplay, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -80f), new Vector2(170f, 44f));
            Button relocateButton = UIButton(relocateRT, new Color(0.16f, 0.2f, 0.3f, 0.92f), roundedSprite, "REUBICAR", 18f, White, out _);

            // Botón Ultimate con medidor
            RectTransform ultGlow = UIRect("UltimateReadyGlow", gameplay, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(26f, 26f), new Vector2(170f, 170f));
            UIImage(ultGlow, new Color(0f, 1f, 0.95f, 0.55f), roundedSprite, false);
            RectTransform ultRT = UIRect("UltimateButton", gameplay, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(36f, 36f), new Vector2(150f, 150f));
            Image ultImage = UIImage(ultRT, new Color(0.1f, 0.13f, 0.2f, 0.95f), roundedSprite, true);
            Button ultButton = ultRT.gameObject.AddComponent<Button>();
            ultButton.targetGraphic = ultImage;
            RectTransform ultArea = UIRect("ChargeArea", ultRT, Vector2.zero, Vector2.one, center, Vector2.zero, new Vector2(-12f, -12f));
            RectTransform ultFill = UIRect("ChargeFill", ultArea, Vector2.zero, new Vector2(1f, 0f), center, Vector2.zero, Vector2.zero);
            UIImage(ultFill, new Color(1f, 0.15f, 0.6f, 0.85f), null, false);
            TextMeshProUGUI ultLabel = UILabel("Label", ultRT, "ULTIMATE\n0%", 20f, Color.white, TextAlignmentOptions.Center);
            SetProps(ultimate, ("ultimateButton", ultButton), ("chargeFill", ultFill), ("chargeLabel", ultLabel), ("readyVisualEffect", ultGlow.gameObject));

            // Botón Beat con anillo de pulso
            var bottomRight = new Vector2(1f, 0f);
            RectTransform ringRT = UIRect("BeatPulseRing", gameplay, bottomRight, bottomRight, center, new Vector2(-111f, 111f), new Vector2(150f, 150f));
            UIImage(ringRT, new Color(0f, 1f, 0.95f, 0.35f), circleSprite, false);
            RectTransform beatRT = UIRect("BeatButton", gameplay, bottomRight, bottomRight, center, new Vector2(-111f, 111f), new Vector2(150f, 150f));
            Button beatButton = UIButton(beatRT, Magenta, circleSprite, "BEAT", 32f, Color.white, out _);

            RectTransform feedbackRT = UIRect("RhythmFeedbackText", gameplay, bottomRight, bottomRight, new Vector2(1f, 0f), new Vector2(-24f, 196f), new Vector2(280f, 46f));
            TextMeshProUGUI feedbackText = UIText(feedbackRT, "", 34f, Turquoise, TextAlignmentOptions.MidlineRight);
            RectTransform comboRT = UIRect("ComboText", gameplay, bottomRight, bottomRight, new Vector2(1f, 0f), new Vector2(-24f, 242f), new Vector2(280f, 40f));
            TextMeshProUGUI comboText = UIText(comboRT, "", 26f, new Color(1f, 0.88f, 0.25f), TextAlignmentOptions.MidlineRight);

            RhythmInput rhythm = canvasObj.AddComponent<RhythmInput>();
            SetProps(rhythm, ("beatButton", beatButton), ("pulseRing", ringRT), ("feedbackText", feedbackText), ("comboText", comboText));

            // Barra de jefe
            RectTransform bossBar = UIRect("BossBarContainer", gameplay, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(540f, 40f));
            RectTransform bossFill = UIBar(bossBar, new Color(0.18f, 0.04f, 0.1f, 0.95f), Magenta);
            TextMeshProUGUI bossName = UILabel("BossNameText", bossBar, "JEFE", 20f, Color.white, TextAlignmentOptions.Center, 2f);
            bossBar.gameObject.SetActive(false);

            // ---------- Paneles eventuales ----------
            RectTransform guidance = UIRect("AR_GuidancePanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(820f, 56f));
            UIImage(guidance, PanelColor, roundedSprite, false);
            TextMeshProUGUI guidanceText = UILabel("GuideText", guidance, "Mueve el teléfono despacio apuntando a una mesa o al piso para detectar la superficie.", 20f, Color.white, TextAlignmentOptions.Center);
            guidanceText.fontStyle = FontStyles.Normal;

            RectTransform toast = UIRect("ToastPanel", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(760f, 50f));
            UIImage(toast, new Color(0.03f, 0.04f, 0.08f, 0.9f), roundedSprite, false);
            TextMeshProUGUI toastText = UILabel("ToastText", toast, "", 20f, new Color(0.7f, 1f, 0.97f), TextAlignmentOptions.Center);
            toastText.fontStyle = FontStyles.Normal;
            toast.gameObject.SetActive(false);

            // Selector de torres con tarjetas
            RectTransform selector = UIRect("TowerSelector_Panel", root, center, center, center, Vector2.zero, new Vector2(900f, 430f));
            UIImage(selector, new Color(0.05f, 0.07f, 0.12f, 0.96f), roundedSprite, true);
            RectTransform headerRT = UIRect("Header", selector, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-120f, 44f));
            UIText(headerRT, "ELIGE UNA TORRE-CANTANTE", 28f, Turquoise, TextAlignmentOptions.Center);
            RectTransform closeRT = UIRect("CloseButton", selector, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12f, -12f), new Vector2(54f, 54f));
            Button closeSelector = UIButton(closeRT, new Color(0.8f, 0.15f, 0.3f), roundedSprite, "X", 26f, Color.white, out _);

            TowerSelectorUI selectorUI = canvasObj.AddComponent<TowerSelectorUI>();
            var selectorSo = new SerializedObject(selectorUI);
            selectorSo.FindProperty("panelRoot").objectReferenceValue = selector.gameObject;
            selectorSo.FindProperty("closeButton").objectReferenceValue = closeSelector;
            SerializedProperty cards = selectorSo.FindProperty("towerCards");
            cards.arraySize = towers.Length;

            for (int i = 0; i < towers.Length; i++)
            {
                TowerDef def = towers[i];
                float x = (i - (towers.Length - 1) * 0.5f) * 212f;

                RectTransform card = UIRect($"Card_{def.name}", selector, center, center, center, new Vector2(x, -26f), new Vector2(198f, 320f));
                Image cardImage = UIImage(card, new Color(0.13f, 0.16f, 0.25f, 1f), roundedSprite, true);
                Button cardButton = card.gameObject.AddComponent<Button>();
                cardButton.targetGraphic = cardImage;
                ColorBlock cardColors = cardButton.colors;
                cardColors.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.55f);
                cardButton.colors = cardColors;

                // Retrato: bloque con los colores de la cantante (sustituible por un sprite anime propio)
                RectTransform portrait = UIRect("Portrait", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(170f, 130f));
                UIImage(portrait, def.dress == Ink ? new Color(0.02f, 0.03f, 0.06f) : def.dress, roundedSprite, false);
                RectTransform hairRT = UIRect("Hair", portrait, center, center, center, new Vector2(0f, 8f), new Vector2(104f, 104f));
                UIImage(hairRT, def.hair, circleSprite, false);
                RectTransform faceRT = UIRect("Face", portrait, center, center, center, new Vector2(0f, -4f), new Vector2(74f, 74f));
                UIImage(faceRT, Skin, circleSprite, false);
                RectTransform tailL = UIRect("TwinTail_L", portrait, center, center, center, new Vector2(-62f, -14f), new Vector2(22f, 86f));
                UIImage(tailL, def.hair, roundedSprite, false);
                RectTransform tailR = UIRect("TwinTail_R", portrait, center, center, center, new Vector2(62f, -14f), new Vector2(22f, 86f));
                UIImage(tailR, def.hair, roundedSprite, false);

                RectTransform nameRT = UIRect("Name", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(0f, 36f));
                UIText(nameRT, def.name.ToUpperInvariant(), 26f, def.hair == White ? Turquoise : def.hair, TextAlignmentOptions.Center);
                RectTransform descRT = UIRect("Description", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(-12f, 56f));
                TextMeshProUGUI desc = UIText(descRT, def.description, 17f, White, TextAlignmentOptions.Center);
                desc.fontStyle = FontStyles.Normal;

                RectTransform costBg = UIRect("CostTag", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(150f, 46f));
                UIImage(costBg, new Color(1f, 0.88f, 0.25f), roundedSprite, false);
                TextMeshProUGUI costText = UILabel("Cost", costBg, def.cost.ToString(), 26f, Ink, TextAlignmentOptions.Center, 2f);

                SerializedProperty element = cards.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("type").enumValueIndex = (int)def.type;
                element.FindPropertyRelative("characterName").stringValue = def.name;
                element.FindPropertyRelative("cost").intValue = def.cost;
                element.FindPropertyRelative("towerPrefab").objectReferenceValue = def.prefab;
                element.FindPropertyRelative("cardButton").objectReferenceValue = cardButton;
                element.FindPropertyRelative("costText").objectReferenceValue = costText;
            }
            selectorSo.ApplyModifiedPropertiesWithoutUndo();
            selector.gameObject.SetActive(false);

            // Game Over y Victoria
            Button BuildEndPanel(string name, string title, string subtitle, Color titleColor, string buttonLabel, out GameObject panel)
            {
                RectTransform overlay = UIRect(name, root, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
                UIImage(overlay, new Color(0f, 0f, 0f, 0.6f), null, true);
                RectTransform box = UIRect("Box", overlay, center, center, center, Vector2.zero, new Vector2(640f, 340f));
                UIImage(box, new Color(0.05f, 0.07f, 0.12f, 0.98f), roundedSprite, false);
                RectTransform titleRT = UIRect("Title", box, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-40f, 70f));
                UIText(titleRT, title, 44f, titleColor, TextAlignmentOptions.Center);
                RectTransform subRT = UIRect("Subtitle", box, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-60f, 70f));
                UIText(subRT, subtitle, 22f, White, TextAlignmentOptions.Center).fontStyle = FontStyles.Normal;
                RectTransform buttonRT = UIRect("RestartButton", box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(300f, 72f));
                Button button = UIButton(buttonRT, titleColor, roundedSprite, buttonLabel, 26f, Ink, out _);
                panel = overlay.gameObject;
                panel.SetActive(false);
                return button;
            }

            Button restartGameOver = BuildEndPanel("GameOverPanel", "GAME OVER", "El Ánimo del escenario llegó a 0.\nLos Glitches arruinaron el concierto.", Magenta, "REINICIAR", out GameObject gameOverPanel);
            Button restartVictory = BuildEndPanel("VictoryPanel", "¡VICTORIA!", "Derrotaste a los cuatro jefes.\nEl concierto está a salvo.", Turquoise, "JUGAR DE NUEVO", out GameObject victoryPanel);

            HUDController hud = canvasObj.AddComponent<HUDController>();
            SetProps(hud,
                ("arGuidancePanel", guidance.gameObject), ("arGuidanceText", guidanceText),
                ("gameplayHudRoot", gameplay.gameObject), ("coinsText", coinsText), ("stageHealthText", healthText),
                ("stageHealthFill", healthFill), ("waveText", waveText),
                ("startWaveButton", startButton), ("startWaveLabel", startLabel), ("relocateButton", relocateButton),
                ("bossBarContainer", bossBar.gameObject), ("bossNameText", bossName), ("bossHealthFill", bossFill),
                ("toastPanel", toast.gameObject), ("toastText", toastText),
                ("gameOverPanel", gameOverPanel), ("restartGameOverButton", restartGameOver),
                ("victoryPanel", victoryPanel), ("restartVictoryButton", restartVictory));
        }

        private static void BuildWorldSpaceTowerMenu()
        {
            var center = new Vector2(0.5f, 0.5f);

            var menuObj = new GameObject("WorldSpace_TowerMenu", typeof(RectTransform)) { layer = 5 };
            Canvas canvas = menuObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            menuObj.AddComponent<GraphicRaycaster>();
            var menuRT = (RectTransform)menuObj.transform;
            menuRT.sizeDelta = new Vector2(420f, 250f);
            menuRT.localScale = Vector3.one * 0.001f;

            UIFollow follow = menuObj.AddComponent<UIFollow>();
            SetProps(follow, ("destroyWithTarget", false), ("keepConstantScreenSize", true), ("sizeMultiplier", 0.0007f), ("lockYAxisOnly", false));

            RectTransform rootRT = UIRect("MenuRoot", menuRT, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            UIImage(rootRT, new Color(0.05f, 0.07f, 0.12f, 0.95f), roundedSprite, true);

            RectTransform nameRT = UIRect("TowerName", rootRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-120f, 46f));
            TextMeshProUGUI nameText = UIText(nameRT, "Bass", 36f, Turquoise, TextAlignmentOptions.Center);
            RectTransform levelRT = UIRect("LevelText", rootRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(-20f, 34f));
            TextMeshProUGUI levelText = UIText(levelRT, "Nivel 1 / 3", 26f, White, TextAlignmentOptions.Center);
            RectTransform statsRT = UIRect("StatsText", rootRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(-20f, 30f));
            TextMeshProUGUI statsText = UIText(statsRT, "Daño 34   Alcance 50", 22f, new Color(0.7f, 1f, 0.97f), TextAlignmentOptions.Center);
            statsText.fontStyle = FontStyles.Normal;

            RectTransform upgradeRT = UIRect("UpgradeButton", rootRT, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(186f, 92f));
            Button upgradeButton = UIButton(upgradeRT, Turquoise, roundedSprite, "MEJORAR\n60", 24f, Ink, out TextMeshProUGUI upgradeText);
            RectTransform sellRT = UIRect("SellButton", rootRT, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(186f, 92f));
            Button sellButton = UIButton(sellRT, Magenta, roundedSprite, "VENDER\n+30", 24f, Color.white, out TextMeshProUGUI sellText);
            RectTransform closeRT = UIRect("CloseButton", rootRT, Vector2.one, Vector2.one, Vector2.one, new Vector2(-8f, -8f), new Vector2(52f, 52f));
            Button closeButton = UIButton(closeRT, new Color(0.25f, 0.28f, 0.38f), roundedSprite, "X", 26f, Color.white, out _);

            TowerMenu menu = menuObj.AddComponent<TowerMenu>();
            SetProps(menu,
                ("menuRoot", rootRT.gameObject), ("uiFollow", follow), ("towerNameText", nameText), ("levelText", levelText),
                ("statsText", statsText), ("upgradeCostText", upgradeText), ("sellRefundText", sellText),
                ("upgradeButton", upgradeButton), ("sellButton", sellButton), ("closeButton", closeButton));

            rootRT.gameObject.SetActive(false);
        }

        #endregion

        #region Ajustes del proyecto

        private static void ApplyProjectSettings()
        {
            // El HUD está diseñado en horizontal
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // La plantilla exigía Android 14 (API 34); se baja a Android 10 para poder instalar en más teléfonos con ARCore
            int current = (int)PlayerSettings.Android.minSdkVersion;
            if (current > 29)
            {
                int[] available = Enum.GetValues(typeof(AndroidSdkVersions)).Cast<int>().Where(v => v >= 29).OrderBy(v => v).ToArray();
                if (available.Length > 0)
                {
                    PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)available[0];
                    Debug.Log($"[ConcertDefense] Android minSdkVersion: {current} -> {available[0]}");
                }
            }
        }

        #endregion
    }
}
