using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using ConcertDefense.Core;
using ConcertDefense.AR;
using ConcertDefense.Towers;
using ConcertDefense.Enemies;
using ConcertDefense.Player;
using ConcertDefense.Rhythm;
using ConcertDefense.UI;

namespace ConcertDefense.EditorTools
{
    public static class GameSetupUtility
    {
        private const string MATERIALS_PATH = "Assets/Materials";
        private const string PREFABS_PATH = "Assets/Prefabs";
        private const string AUDIO_PATH = "Assets/Audio";

        [MenuItem("ConcertDefense/Setup Complete Game (Generate All Assets & Scene)", priority = 1)]
        public static void SetupCompleteGame()
        {
            Debug.Log("[ConcertDefense] Iniciando montaje completo del juego...");
            CreateDirectories();

            AudioClip bgmClip = GenerateSynthBGM();
            AudioClip sfxTeleport = GenerateBeepSFX("TeleportSFX.wav", 600f, 1200f, 0.3f);
            AudioClip sfxFeedback = GenerateBeepSFX("FeedbackSFX.wav", 880f, 440f, 0.5f);

            Dictionary<string, Material> materials = CreateMaterials();
            var projectiles = CreateProjectiles(materials);
            var towers = CreateTowers(materials, projectiles);
            var enemies = CreateEnemiesAndBosses(materials, sfxFeedback);

            GameObject avatarPrefab = CreateAvatarPrefab(materials);
            GameObject reticlePrefab = CreateReticlePrefab(materials);
            GameObject battlefieldPrefab = CreateBattlefieldPrefab(materials, avatarPrefab, sfxTeleport);

            AssembleScene(bgmClip, battlefieldPrefab, reticlePrefab, towers, enemies);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFFF><b>[ConcertDefense] ¡Montaje completado con éxito! Todos los prefabs, audio, materiales, HUD y la escena han sido configurados.</b></color>");
        }

        private static void CreateDirectories()
        {
            string[] dirs = {
                MATERIALS_PATH,
                AUDIO_PATH,
                PREFABS_PATH,
                $"{PREFABS_PATH}/Towers",
                $"{PREFABS_PATH}/Projectiles",
                $"{PREFABS_PATH}/Enemies",
                $"{PREFABS_PATH}/Bosses",
                $"{PREFABS_PATH}/Environment"
            };

            foreach (var dir in dirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
                    string folderName = Path.GetFileName(dir);
                    AssetDatabase.CreateFolder(parent, folderName);
                }
            }
        }

        #region Material Generation
        private static Dictionary<string, Material> CreateMaterials()
        {
            var dict = new Dictionary<string, Material>();
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpShader == null) urpShader = Shader.Find("Standard");

            Material CreateMat(string name, Color baseColor, Color emissionColor, float smoothness = 0.5f, bool transparent = false)
            {
                string path = $"{MATERIALS_PATH}/{name}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(urpShader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.color = baseColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emissionColor);
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

                if (transparent)
                {
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }

                EditorUtility.SetDirty(mat);
                dict[name] = mat;
                return mat;
            }

            CreateMat("Mat_NeonCyan", new Color(0f, 1f, 0.95f), new Color(0f, 0.8f, 0.75f) * 2f, 0.8f);
            CreateMat("Mat_NeonMagenta", new Color(1f, 0.1f, 0.7f), new Color(0.9f, 0f, 0.6f) * 2f, 0.8f);
            CreateMat("Mat_NeonYellow", new Color(1f, 0.92f, 0.15f), new Color(0.9f, 0.8f, 0f) * 2f, 0.8f);
            CreateMat("Mat_NeonPurple", new Color(0.7f, 0.2f, 1f), new Color(0.6f, 0f, 0.9f) * 2f, 0.8f);
            CreateMat("Mat_DarkStage", new Color(0.12f, 0.12f, 0.18f), Color.black, 0.4f);
            CreateMat("Mat_FieldPath", new Color(0.2f, 0.22f, 0.28f), new Color(0f, 0.2f, 0.3f) * 0.5f, 0.6f);
            CreateMat("Mat_BuildSpotNormal", new Color(0.2f, 0.8f, 1f, 0.7f), new Color(0f, 0.5f, 0.7f) * 1.5f, 0.7f, true);
            CreateMat("Mat_BuildSpotInRange", new Color(0f, 1f, 0.5f, 0.8f), new Color(0f, 1f, 0.4f) * 2f, 0.8f, true);
            CreateMat("Mat_BuildSpotOutOfRange", new Color(1f, 0.2f, 0.2f, 0.8f), new Color(1f, 0.1f, 0.1f) * 2f, 0.8f, true);
            CreateMat("Mat_NoiseZone", new Color(0.8f, 0.1f, 0.9f, 0.35f), new Color(0.6f, 0f, 0.8f), 0.2f, true);
            CreateMat("Mat_Reticle", new Color(0f, 1f, 0.9f, 0.75f), new Color(0f, 1f, 0.8f) * 2.5f, 0.9f, true);
            CreateMat("Mat_WhiteMetal", new Color(0.95f, 0.95f, 0.98f), new Color(0.1f, 0.1f, 0.15f), 0.7f);
            CreateMat("Mat_EnemyPixel", new Color(0f, 0.9f, 1f), new Color(0f, 0.8f, 1f) * 1.5f, 0.7f);
            CreateMat("Mat_EnemyStatic", new Color(1f, 0.2f, 0.5f), new Color(1f, 0.1f, 0.4f) * 1.5f, 0.7f);
            CreateMat("Mat_EnemyAmp", new Color(1f, 0.85f, 0.2f), new Color(1f, 0.75f, 0f) * 1.5f, 0.7f);
            CreateMat("Mat_BossDark", new Color(0.15f, 0.05f, 0.25f), new Color(0.8f, 0f, 0.8f) * 1.5f, 0.8f);

            return dict;
        }
        #endregion

        #region Audio Generation
        private static AudioClip GenerateSynthBGM()
        {
            string filePath = $"{AUDIO_PATH}/BGM_120BPM_CyberConcert.wav";
            int sampleRate = 44100;
            float bpm = 120f;
            float beatDuration = 60f / bpm; // 0.5 sec
            int totalBeats = 16; // 4 bars = 8 seconds loop
            int totalSamples = (int)(sampleRate * totalBeats * beatDuration);

            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float time = (float)i / sampleRate;
                float beatTime = time % beatDuration;
                int currentBeat = (int)(time / beatDuration);

                // 1. Kick en cada pulso (compás de 4/4)
                float kickFreq = Mathf.Lerp(150f, 40f, Mathf.Clamp01(beatTime / 0.15f));
                float kickEnv = Mathf.Exp(-beatTime * 14f);
                float kick = Mathf.Sin(2f * Mathf.PI * kickFreq * beatTime) * kickEnv * 0.7f;

                // 2. Hi-Hat en octavas (cada 0.25 s)
                float hatTime = time % (beatDuration * 0.5f);
                float hatEnv = Mathf.Exp(-hatTime * 45f);
                float hat = (Random.value * 2f - 1f) * hatEnv * 0.18f;

                // 3. Synth Bassline melódica en 120 BPM
                float[] notes = { 110f, 130.81f, 146.83f, 164.81f }; // A2, C3, D3, E3
                float currentFreq = notes[(currentBeat / 2) % notes.Length];
                float bassEnv = Mathf.Exp(-beatTime * 4f);
                float bass = Mathf.Sin(2f * Mathf.PI * currentFreq * time) * bassEnv * 0.35f;

                samples[i] = Mathf.Clamp(kick + hat + bass, -1f, 1f);
            }

            WriteWavFile(filePath, samples, sampleRate);
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(filePath);
        }

        private static AudioClip GenerateBeepSFX(string fileName, float startFreq, float endFreq, float duration)
        {
            string filePath = $"{AUDIO_PATH}/{fileName}";
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                float env = Mathf.Sin(t * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * env * 0.6f;
            }

            WriteWavFile(filePath, samples, sampleRate);
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(filePath);
        }

        private static void WriteWavFile(string path, float[] samples, int sampleRate)
        {
            byte[] wav = new byte[44 + samples.Length * 2];
            int byteRate = sampleRate * 2;

            // RIFF header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            System.BitConverter.GetBytes(wav.Length - 8).CopyTo(wav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            System.BitConverter.GetBytes(16).CopyTo(wav, 16);
            System.BitConverter.GetBytes((short)1).CopyTo(wav, 20); // PCM
            System.BitConverter.GetBytes((short)1).CopyTo(wav, 22); // Mono
            System.BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
            System.BitConverter.GetBytes(byteRate).CopyTo(wav, 28);
            System.BitConverter.GetBytes((short)2).CopyTo(wav, 32); // Block align
            System.BitConverter.GetBytes((short)16).CopyTo(wav, 34); // Bits per sample
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            System.BitConverter.GetBytes(samples.Length * 2).CopyTo(wav, 40);

            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                System.BitConverter.GetBytes(val).CopyTo(wav, 44 + i * 2);
            }

            File.WriteAllBytes(path, wav);
        }
        #endregion

        #region Projectile Prefabs
        private static Dictionary<TowerType, GameObject> CreateProjectiles(Dictionary<string, Material> mats)
        {
            var dict = new Dictionary<TowerType, GameObject>();

            GameObject CreateProj(string name, TowerType type, Material mat, PrimitiveType prim, Vector3 scale, float spd, float dmg, float splash = 0f, float knock = 0f, float slow = 1f, float slowDur = 0f)
            {
                string path = $"{PREFABS_PATH}/Projectiles/{name}.prefab";
                GameObject go = GameObject.CreatePrimitive(prim);
                go.name = name;
                go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;

                Collider col = go.GetComponent<Collider>();
                col.isTrigger = true;

                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;

                Projectile proj = go.AddComponent<Projectile>();
                // Initialize default parameters
                proj.Initialize(null, dmg, spd - 6f, splash, knock, slow, slowDur);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                GameObject.DestroyImmediate(go);
                dict[type] = prefab;
                return prefab;
            }

            CreateProj("Projectile_Bass", TowerType.Bass, mats["Mat_NeonCyan"], PrimitiveType.Sphere, Vector3.one * 0.12f, 8f, 30f);
            CreateProj("Projectile_Treble", TowerType.Treble, mats["Mat_NeonYellow"], PrimitiveType.Sphere, Vector3.one * 0.08f, 14f, 15f);
            CreateProj("Projectile_Echo", TowerType.Echo, mats["Mat_NeonMagenta"], PrimitiveType.Cylinder, new Vector3(0.18f, 0.03f, 0.18f), 7f, 20f, 0.65f, 0f, 0.4f, 2.5f);
            CreateProj("Projectile_Drop", TowerType.Drop, mats["Mat_NeonPurple"], PrimitiveType.Cube, Vector3.one * 0.15f, 7f, 45f, 0.6f, 12f);

            return dict;
        }
        #endregion

        #region Tower Prefabs
        private static Dictionary<TowerType, GameObject> CreateTowers(Dictionary<string, Material> mats, Dictionary<TowerType, GameObject> projs)
        {
            var dict = new Dictionary<TowerType, GameObject>();

            GameObject CreateTower(TowerType type, string name, int cost, Material accentMat, GameObject projPrefab, TowerStats[] stats)
            {
                string path = $"{PREFABS_PATH}/Towers/{name}.prefab";
                GameObject root = new GameObject(name);
                root.tag = "Tower";

                // Base física
                BoxCollider boxCol = root.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.25f, 0.4f, 0.25f);
                boxCol.center = new Vector3(0f, 0.2f, 0f);

                // Malla de pedestal
                GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.name = "Pedestal";
                pedestal.transform.SetParent(root.transform);
                pedestal.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                pedestal.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);
                pedestal.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_DarkStage"];
                GameObject.DestroyImmediate(pedestal.GetComponent<Collider>());

                // Cabeza giratoria (RotatorPart)
                GameObject rotator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rotator.name = "RotatorHead";
                rotator.transform.SetParent(root.transform);
                rotator.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                rotator.transform.localScale = new Vector3(0.15f, 0.15f, 0.2f);
                rotator.GetComponent<MeshRenderer>().sharedMaterial = accentMat;
                GameObject.DestroyImmediate(rotator.GetComponent<Collider>());

                // Cañón / Speaker cone
                GameObject speaker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                speaker.name = "SpeakerCone";
                speaker.transform.SetParent(rotator.transform);
                speaker.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                speaker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                speaker.transform.localScale = new Vector3(0.6f, 0.25f, 0.6f);
                speaker.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_WhiteMetal"];
                GameObject.DestroyImmediate(speaker.GetComponent<Collider>());

                // Fire Point
                GameObject firePointObj = new GameObject("FirePoint");
                firePointObj.transform.SetParent(rotator.transform);
                firePointObj.transform.localPosition = new Vector3(0f, 0f, 0.8f);

                // Range visualizer disc
                GameObject rangeVis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rangeVis.name = "RangeVisualizer";
                rangeVis.transform.SetParent(root.transform);
                rangeVis.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                rangeVis.transform.localScale = new Vector3(2.4f, 0.002f, 2.4f);
                rangeVis.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_BuildSpotNormal"];
                GameObject.DestroyImmediate(rangeVis.GetComponent<Collider>());
                rangeVis.SetActive(false);

                // Silence Indicator
                GameObject silenceInd = GameObject.CreatePrimitive(PrimitiveType.Cube);
                silenceInd.name = "SilenceIndicator";
                silenceInd.transform.SetParent(root.transform);
                silenceInd.transform.localPosition = new Vector3(0f, 0.38f, 0f);
                silenceInd.transform.localScale = Vector3.one * 0.08f;
                silenceInd.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonMagenta"];
                GameObject.DestroyImmediate(silenceInd.GetComponent<Collider>());
                silenceInd.SetActive(false);

                // Componente Tower
                Tower tower = root.AddComponent<Tower>();
                var so = new SerializedObject(tower);
                so.FindProperty("towerType").enumValueIndex = (int)type;
                so.FindProperty("towerName").stringValue = name;
                so.FindProperty("baseCost").intValue = cost;
                so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
                so.FindProperty("firePoint").objectReferenceValue = firePointObj.transform;
                so.FindProperty("rotatorPart").objectReferenceValue = rotator.transform;
                so.FindProperty("rangeVisualizer").objectReferenceValue = rangeVis;
                so.FindProperty("silenceIndicator").objectReferenceValue = silenceInd;

                var statsArr = so.FindProperty("statsPerLevel");
                statsArr.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var elem = statsArr.GetArrayElementAtIndex(i);
                    elem.FindPropertyRelative("damage").floatValue = stats[i].damage;
                    elem.FindPropertyRelative("range").floatValue = stats[i].range;
                    elem.FindPropertyRelative("fireRate").floatValue = stats[i].fireRate;
                    elem.FindPropertyRelative("upgradeCost").intValue = stats[i].upgradeCost;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                GameObject.DestroyImmediate(root);
                dict[type] = prefab;
                return prefab;
            }

            // Bass (50)
            dict[TowerType.Bass] = CreateTower(TowerType.Bass, "Tower_Bass", 50, mats["Mat_NeonCyan"], projs[TowerType.Bass],
                new TowerStats[] {
                    new TowerStats { damage = 30f, range = 1.2f, fireRate = 1.0f, upgradeCost = 60 },
                    new TowerStats { damage = 50f, range = 1.5f, fireRate = 1.2f, upgradeCost = 90 },
                    new TowerStats { damage = 85f, range = 1.8f, fireRate = 1.5f, upgradeCost = 0 }
                });

            // Treble (40)
            dict[TowerType.Treble] = CreateTower(TowerType.Treble, "Tower_Treble", 40, mats["Mat_NeonYellow"], projs[TowerType.Treble],
                new TowerStats[] {
                    new TowerStats { damage = 15f, range = 1.5f, fireRate = 2.5f, upgradeCost = 50 },
                    new TowerStats { damage = 25f, range = 1.8f, fireRate = 3.0f, upgradeCost = 80 },
                    new TowerStats { damage = 45f, range = 2.2f, fireRate = 4.0f, upgradeCost = 0 }
                });

            // Echo (60)
            dict[TowerType.Echo] = CreateTower(TowerType.Echo, "Tower_Echo", 60, mats["Mat_NeonMagenta"], projs[TowerType.Echo],
                new TowerStats[] {
                    new TowerStats { damage = 20f, range = 1.3f, fireRate = 0.8f, upgradeCost = 70 },
                    new TowerStats { damage = 35f, range = 1.6f, fireRate = 1.0f, upgradeCost = 100 },
                    new TowerStats { damage = 60f, range = 2.0f, fireRate = 1.3f, upgradeCost = 0 }
                });

            // Drop (80)
            dict[TowerType.Drop] = CreateTower(TowerType.Drop, "Tower_Drop", 80, mats["Mat_NeonPurple"], projs[TowerType.Drop],
                new TowerStats[] {
                    new TowerStats { damage = 45f, range = 1.0f, fireRate = 0.7f, upgradeCost = 90 },
                    new TowerStats { damage = 75f, range = 1.3f, fireRate = 0.9f, upgradeCost = 130 },
                    new TowerStats { damage = 120f, range = 1.6f, fireRate = 1.2f, upgradeCost = 0 }
                });

            return dict;
        }
        #endregion

        #region Enemies & Bosses Prefabs
        private static Dictionary<string, GameObject> CreateEnemiesAndBosses(Dictionary<string, Material> mats, AudioClip sfxFeedback)
        {
            var dict = new Dictionary<string, GameObject>();

            // NoiseZone Prefab
            GameObject nzObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nzObj.name = "NoiseZone";
            nzObj.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            nzObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NoiseZone"];
            var nzCol = nzObj.GetComponent<Collider>();
            nzCol.isTrigger = true;
            nzObj.AddComponent<NoiseZone>();
            GameObject nzPrefab = PrefabUtility.SaveAsPrefabAsset(nzObj, $"{PREFABS_PATH}/Enemies/NoiseZone.prefab");
            GameObject.DestroyImmediate(nzObj);
            dict["NoiseZone"] = nzPrefab;

            // 1. Enemy_Pixel
            GameObject pixelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pixelObj.name = "Enemy_Pixel";
            pixelObj.tag = "Enemy";
            pixelObj.transform.localScale = Vector3.one * 0.16f;
            pixelObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_EnemyPixel"];
            Rigidbody rbP = pixelObj.AddComponent<Rigidbody>();
            rbP.useGravity = false;
            rbP.isKinematic = true;
            Enemy ep = pixelObj.AddComponent<Enemy>();
            var soP = new SerializedObject(ep);
            soP.FindProperty("glitchName").stringValue = "Pixel";
            soP.FindProperty("maxHealth").floatValue = 60f;
            soP.FindProperty("moveSpeed").floatValue = 1.4f;
            soP.FindProperty("coinsReward").intValue = 10;
            soP.FindProperty("stageDamage").intValue = 1;
            soP.ApplyModifiedPropertiesWithoutUndo();
            GameObject pixelPrefab = PrefabUtility.SaveAsPrefabAsset(pixelObj, $"{PREFABS_PATH}/Enemies/Enemy_Pixel.prefab");
            GameObject.DestroyImmediate(pixelObj);
            dict["Enemy_Pixel"] = pixelPrefab;

            // 2. Enemy_Static
            GameObject staticObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            staticObj.name = "Enemy_Static";
            staticObj.tag = "Enemy";
            staticObj.transform.localScale = Vector3.one * 0.22f;
            staticObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_EnemyStatic"];
            Rigidbody rbS = staticObj.AddComponent<Rigidbody>();
            rbS.useGravity = false;
            rbS.isKinematic = true;
            Enemy es = staticObj.AddComponent<Enemy>();
            var soS = new SerializedObject(es);
            soS.FindProperty("glitchName").stringValue = "Static";
            soS.FindProperty("maxHealth").floatValue = 120f;
            soS.FindProperty("moveSpeed").floatValue = 1.0f;
            soS.FindProperty("coinsReward").intValue = 20;
            soS.FindProperty("stageDamage").intValue = 1;
            soS.ApplyModifiedPropertiesWithoutUndo();
            GameObject staticPrefab = PrefabUtility.SaveAsPrefabAsset(staticObj, $"{PREFABS_PATH}/Enemies/Enemy_Static.prefab");
            GameObject.DestroyImmediate(staticObj);
            dict["Enemy_Static"] = staticPrefab;

            // 3. Enemy_Amp
            GameObject ampObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ampObj.name = "Enemy_Amp";
            ampObj.tag = "Enemy";
            ampObj.transform.localScale = new Vector3(0.24f, 0.18f, 0.24f);
            ampObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_EnemyAmp"];
            Rigidbody rbA = ampObj.AddComponent<Rigidbody>();
            rbA.useGravity = false;
            rbA.isKinematic = true;
            Enemy ea = ampObj.AddComponent<Enemy>();
            var soA = new SerializedObject(ea);
            soA.FindProperty("glitchName").stringValue = "Amp";
            soA.FindProperty("maxHealth").floatValue = 280f;
            soA.FindProperty("moveSpeed").floatValue = 0.75f;
            soA.FindProperty("coinsReward").intValue = 35;
            soA.FindProperty("stageDamage").intValue = 2;
            soA.ApplyModifiedPropertiesWithoutUndo();
            GameObject ampPrefab = PrefabUtility.SaveAsPrefabAsset(ampObj, $"{PREFABS_PATH}/Enemies/Enemy_Amp.prefab");
            GameObject.DestroyImmediate(ampObj);
            dict["Enemy_Amp"] = ampPrefab;

            // BOSS 1: Distortion
            GameObject bdObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bdObj.name = "Boss_Distortion";
            bdObj.tag = "Enemy";
            bdObj.transform.localScale = Vector3.one * 0.38f;
            bdObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_BossDark"];
            Rigidbody rbBD = bdObj.AddComponent<Rigidbody>();
            rbBD.useGravity = false;
            rbBD.isKinematic = true;
            BossDistortion bDist = bdObj.AddComponent<BossDistortion>();
            var soBD = new SerializedObject(bDist);
            soBD.FindProperty("glitchName").stringValue = "Distorsión";
            soBD.FindProperty("bossTitle").stringValue = "Distorsión - Corruptor de Ondas";
            soBD.FindProperty("maxHealth").floatValue = 600f;
            soBD.FindProperty("moveSpeed").floatValue = 0.65f;
            soBD.FindProperty("coinsReward").intValue = 100;
            soBD.FindProperty("noiseZonePrefab").objectReferenceValue = nzPrefab;
            soBD.ApplyModifiedPropertiesWithoutUndo();
            dict["Boss_Distortion"] = PrefabUtility.SaveAsPrefabAsset(bdObj, $"{PREFABS_PATH}/Bosses/Boss_Distortion.prefab");
            GameObject.DestroyImmediate(bdObj);

            // BOSS 2: Feedback
            GameObject bfObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bfObj.name = "Boss_Feedback";
            bfObj.tag = "Enemy";
            bfObj.transform.localScale = Vector3.one * 0.42f;
            bfObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonMagenta"];
            Rigidbody rbBF = bfObj.AddComponent<Rigidbody>();
            rbBF.useGravity = false;
            rbBF.isKinematic = true;
            BossFeedback bFeed = bfObj.AddComponent<BossFeedback>();
            var soBF = new SerializedObject(bFeed);
            soBF.FindProperty("glitchName").stringValue = "Feedback";
            soBF.FindProperty("bossTitle").stringValue = "Feedback - Acople Acústico";
            soBF.FindProperty("maxHealth").floatValue = 800f;
            soBF.FindProperty("moveSpeed").floatValue = 0.75f;
            soBF.FindProperty("coinsReward").intValue = 140;
            soBF.FindProperty("silenceRadius").floatValue = 2.0f;
            soBF.FindProperty("silenceDuration").floatValue = 6.0f;
            soBF.FindProperty("feedbackSfx").objectReferenceValue = sfxFeedback;
            soBF.ApplyModifiedPropertiesWithoutUndo();
            dict["Boss_Feedback"] = PrefabUtility.SaveAsPrefabAsset(bfObj, $"{PREFABS_PATH}/Bosses/Boss_Feedback.prefab");
            GameObject.DestroyImmediate(bfObj);

            // BOSS 3: Glitch Queen
            GameObject bqObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bqObj.name = "Boss_GlitchQueen";
            bqObj.tag = "Enemy";
            bqObj.transform.localScale = new Vector3(0.42f, 0.4f, 0.42f);
            bqObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonCyan"];
            Rigidbody rbBQ = bqObj.AddComponent<Rigidbody>();
            rbBQ.useGravity = false;
            rbBQ.isKinematic = true;
            BossGlitchQueen bQueen = bqObj.AddComponent<BossGlitchQueen>();
            var soBQ = new SerializedObject(bQueen);
            soBQ.FindProperty("glitchName").stringValue = "Reina Glitch";
            soBQ.FindProperty("bossTitle").stringValue = "Reina Glitch - Reina de Enjambre";
            soBQ.FindProperty("maxHealth").floatValue = 1000f;
            soBQ.FindProperty("moveSpeed").floatValue = 0.7f;
            soBQ.FindProperty("coinsReward").intValue = 180;
            soBQ.FindProperty("minionPrefab").objectReferenceValue = pixelPrefab;
            soBQ.ApplyModifiedPropertiesWithoutUndo();
            dict["Boss_GlitchQueen"] = PrefabUtility.SaveAsPrefabAsset(bqObj, $"{PREFABS_PATH}/Bosses/Boss_GlitchQueen.prefab");
            GameObject.DestroyImmediate(bqObj);

            // BOSS 4: Final Mix
            GameObject bfmObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bfmObj.name = "Boss_FinalMix";
            bfmObj.tag = "Enemy";
            bfmObj.transform.localScale = Vector3.one * 0.5f;
            bfmObj.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonYellow"];
            Rigidbody rbBFM = bfmObj.AddComponent<Rigidbody>();
            rbBFM.useGravity = false;
            rbBFM.isKinematic = true;
            BossFinalMix bMix = bfmObj.AddComponent<BossFinalMix>();
            var soBFM = new SerializedObject(bMix);
            soBFM.FindProperty("glitchName").stringValue = "Mezcla Final";
            soBFM.FindProperty("bossTitle").stringValue = "Mezcla Final - Sobrecarga Maestra";
            soBFM.FindProperty("maxHealth").floatValue = 1500f;
            soBFM.FindProperty("moveSpeed").floatValue = 0.6f;
            soBFM.FindProperty("coinsReward").intValue = 250;
            soBFM.FindProperty("noiseZonePrefab").objectReferenceValue = nzPrefab;
            soBFM.FindProperty("minionPrefab").objectReferenceValue = pixelPrefab;
            soBFM.ApplyModifiedPropertiesWithoutUndo();
            dict["Boss_FinalMix"] = PrefabUtility.SaveAsPrefabAsset(bfmObj, $"{PREFABS_PATH}/Bosses/Boss_FinalMix.prefab");
            GameObject.DestroyImmediate(bfmObj);

            return dict;
        }
        #endregion

        #region Avatar & Reticle Prefabs
        private static GameObject CreateAvatarPrefab(Dictionary<string, Material> mats)
        {
            string path = $"{PREFABS_PATH}/Environment/Avatar_Hero.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Avatar_Hero";
            root.tag = "Avatar";
            root.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
            root.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonCyan"];

            // Visor anime
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            visor.transform.SetParent(root.transform);
            visor.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            visor.transform.localScale = new Vector3(0.7f, 0.25f, 0.35f);
            visor.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonMagenta"];
            GameObject.DestroyImmediate(visor.GetComponent<Collider>());

            AvatarController ac = root.AddComponent<AvatarController>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            GameObject.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateReticlePrefab(Dictionary<string, Material> mats)
        {
            string path = $"{PREFABS_PATH}/Environment/PlacementReticle.prefab";
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PlacementReticle";
            ring.transform.localScale = new Vector3(0.8f, 0.005f, 0.8f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_Reticle"];
            GameObject.DestroyImmediate(ring.GetComponent<Collider>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ring, path);
            GameObject.DestroyImmediate(ring);
            return prefab;
        }
        #endregion

        #region Battlefield Prefab
        private static GameObject CreateBattlefieldPrefab(Dictionary<string, Material> mats, GameObject avatarPrefab, AudioClip sfxTeleport)
        {
            string path = $"{PREFABS_PATH}/Environment/Battlefield.prefab";
            GameObject bfRoot = new GameObject("Battlefield");

            // 1. Suelo Principal
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.tag = "Field";
            ground.transform.SetParent(bfRoot.transform);
            ground.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(1.6f, 0.04f, 1.6f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_DarkStage"];

            // 2. Trazado visual del camino
            Vector3[] pathPoints = new Vector3[] {
                new Vector3(-0.6f, 0.01f, -0.6f),
                new Vector3(-0.6f, 0.01f, -0.2f),
                new Vector3(-0.1f, 0.01f, -0.2f),
                new Vector3(-0.1f, 0.01f, 0.35f),
                new Vector3(0.35f, 0.01f, 0.35f),
                new Vector3(0.35f, 0.01f, -0.1f),
                new Vector3(0.65f, 0.01f, -0.1f),
                new Vector3(0.65f, 0.01f, 0.55f)
            };

            GameObject pathContainer = new GameObject("Path");
            pathContainer.transform.SetParent(bfRoot.transform);

            for (int i = 0; i < pathPoints.Length; i++)
            {
                GameObject wp = new GameObject($"Waypoint_{i}");
                wp.transform.SetParent(pathContainer.transform);
                wp.transform.localPosition = pathPoints[i];

                // Línea / baldosa visual sobre el camino
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tile.name = $"Tile_{i}";
                tile.transform.SetParent(wp.transform);
                tile.transform.localPosition = Vector3.zero;
                tile.transform.localScale = new Vector3(0.25f, 0.005f, 0.25f);
                tile.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_FieldPath"];
                GameObject.DestroyImmediate(tile.GetComponent<Collider>());
            }

            // Escenario / Meta final (DJ Stage)
            GameObject stageGoal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stageGoal.name = "DJ_Stage_Goal";
            stageGoal.transform.SetParent(bfRoot.transform);
            stageGoal.transform.localPosition = new Vector3(0.65f, 0.06f, 0.55f);
            stageGoal.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
            stageGoal.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonCyan"];
            GameObject.DestroyImmediate(stageGoal.GetComponent<Collider>());

            // 3. BuildSpots (Plataformas de Construcción)
            Vector3[] spotPositions = new Vector3[] {
                new Vector3(-0.35f, 0.02f, -0.45f),
                new Vector3(-0.35f, 0.02f, 0.1f),
                new Vector3(0.12f, 0.02f, 0.1f),
                new Vector3(0.12f, 0.02f, 0.55f),
                new Vector3(0.55f, 0.02f, 0.2f)
            };

            for (int i = 0; i < spotPositions.Length; i++)
            {
                GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spot.name = $"BuildSpot_{i + 1}";
                spot.tag = "BuildSpot";
                spot.transform.SetParent(bfRoot.transform);
                spot.transform.localPosition = spotPositions[i];
                spot.transform.localScale = new Vector3(0.28f, 0.02f, 0.28f);
                spot.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_BuildSpotNormal"];

                BuildSpot bs = spot.AddComponent<BuildSpot>();
                var soBS = new SerializedObject(bs);
                soBS.FindProperty("requireAvatarNearby").boolValue = true;
                soBS.FindProperty("avatarProximityDistance").floatValue = 1.3f;
                soBS.FindProperty("availableIndicator").objectReferenceValue = spot;
                soBS.FindProperty("inRangeColor").colorValue = mats["Mat_BuildSpotInRange"].color;
                soBS.FindProperty("outOfRangeColor").colorValue = mats["Mat_BuildSpotOutOfRange"].color;
                soBS.ApplyModifiedPropertiesWithoutUndo();
            }

            // 4. TeleportPads (2 Plataformas conectadas)
            GameObject padA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padA.name = "TeleportPad_A";
            padA.transform.SetParent(bfRoot.transform);
            padA.transform.localPosition = new Vector3(-0.55f, 0.02f, 0.55f);
            padA.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);
            padA.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonYellow"];

            GameObject padB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padB.name = "TeleportPad_B";
            padB.transform.SetParent(bfRoot.transform);
            padB.transform.localPosition = new Vector3(0.55f, 0.02f, -0.55f);
            padB.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);
            padB.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonYellow"];

            TeleportPad tpA = padA.AddComponent<TeleportPad>();
            TeleportPad tpB = padB.AddComponent<TeleportPad>();

            var soTPA = new SerializedObject(tpA);
            soTPA.FindProperty("pairedPad").objectReferenceValue = tpB;
            soTPA.FindProperty("teleportSfx").objectReferenceValue = sfxTeleport;
            soTPA.ApplyModifiedPropertiesWithoutUndo();

            var soTPB = new SerializedObject(tpB);
            soTPB.FindProperty("pairedPad").objectReferenceValue = tpA;
            soTPB.FindProperty("teleportSfx").objectReferenceValue = sfxTeleport;
            soTPB.ApplyModifiedPropertiesWithoutUndo();

            // 5. Roller Coaster Track & Cart
            GameObject trackRoot = new GameObject("RollerCoasterTrack");
            trackRoot.transform.SetParent(bfRoot.transform);

            Vector3[] trackPoints = new Vector3[] {
                new Vector3(-0.7f, 0.25f, -0.7f),
                new Vector3(-0.7f, 0.25f, 0.7f),
                new Vector3(0.7f, 0.25f, 0.7f),
                new Vector3(0.7f, 0.25f, -0.7f),
                new Vector3(-0.7f, 0.25f, -0.7f)
            };

            for (int i = 0; i < trackPoints.Length; i++)
            {
                GameObject tw = new GameObject($"TrackWP_{i}");
                tw.transform.SetParent(trackRoot.transform);
                tw.transform.localPosition = trackPoints[i];
            }

            GameObject cart = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cart.name = "RollerCoasterCart";
            cart.transform.SetParent(bfRoot.transform);
            cart.transform.localScale = new Vector3(0.25f, 0.15f, 0.4f);
            cart.GetComponent<MeshRenderer>().sharedMaterial = mats["Mat_NeonMagenta"];
            var cartCol = cart.GetComponent<Collider>();
            cartCol.isTrigger = true;
            cart.AddComponent<RollerCoasterCart>();
            cart.SetActive(false);

            // 6. Contenedor de enemigos instanciados
            GameObject enemiesContainer = new GameObject("EnemiesContainer");
            enemiesContainer.transform.SetParent(bfRoot.transform);

            // 7. Instanciar Avatar dentro de Battlefield
            if (avatarPrefab != null)
            {
                GameObject avatarInstance = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab, bfRoot.transform);
                avatarInstance.name = "Avatar_Hero";
                avatarInstance.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bfRoot, path);
            GameObject.DestroyImmediate(bfRoot);
            return prefab;
        }
        #endregion

        #region Scene Assembly & UI
        private static void AssembleScene(AudioClip bgmClip, GameObject battlefieldPrefab, GameObject reticlePrefab, Dictionary<TowerType, GameObject> towers, Dictionary<string, GameObject> enemies)
        {
            // 1. Limpiar o asegurar GameManager y sistemas en SampleScene
            GameObject managersRoot = GameObject.Find("[GameManagers]");
            if (managersRoot == null)
            {
                managersRoot = new GameObject("[GameManagers]");
            }

            // GameManager
            GameManager gm = managersRoot.GetComponent<GameManager>();
            if (gm == null) gm = managersRoot.AddComponent<GameManager>();

            // WaveSpawner
            WaveSpawner ws = managersRoot.GetComponent<WaveSpawner>();
            if (ws == null) ws = managersRoot.AddComponent<WaveSpawner>();
            ConfigureWaves(ws, enemies);

            // BeatClock
            BeatClock bc = managersRoot.GetComponent<BeatClock>();
            if (bc == null) bc = managersRoot.AddComponent<BeatClock>();
            AudioSource musicSrc = managersRoot.GetComponent<AudioSource>();
            if (musicSrc == null) musicSrc = managersRoot.AddComponent<AudioSource>();
            musicSrc.clip = bgmClip;
            musicSrc.loop = true;
            musicSrc.playOnAwake = false;

            var soBC = new SerializedObject(bc);
            soBC.FindProperty("bpm").floatValue = 120f;
            soBC.FindProperty("musicSource").objectReferenceValue = musicSrc;
            soBC.FindProperty("autoStartOnPlaying").boolValue = true;
            soBC.ApplyModifiedPropertiesWithoutUndo();

            // UltimateController
            UltimateController uc = managersRoot.GetComponent<UltimateController>();
            if (uc == null) uc = managersRoot.AddComponent<UltimateController>();

            // 2. AR Placement & Field Manipulator en XR Origin (AR Rig)
            GameObject arPlacementMgrOld = GameObject.Find("ARPlacementManager");
            if (arPlacementMgrOld != null) GameObject.DestroyImmediate(arPlacementMgrOld);

            GameObject xrOriginObj = GameObject.Find("XR Origin (AR Rig)");
            if (xrOriginObj != null)
            {
                ARPlacementController arPlacer = xrOriginObj.GetComponent<ARPlacementController>();
                if (arPlacer == null) arPlacer = xrOriginObj.AddComponent<ARPlacementController>();

                FieldManipulator fieldManip = xrOriginObj.GetComponent<FieldManipulator>();
                if (fieldManip == null) fieldManip = xrOriginObj.AddComponent<FieldManipulator>();

                LightEstimationController lightEst = xrOriginObj.GetComponent<LightEstimationController>();
                if (lightEst == null) lightEst = xrOriginObj.AddComponent<LightEstimationController>();

                // Limpiar retículas duplicadas anteriores
                foreach (var oldReticle in GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (oldReticle.name == "PlacementReticle")
                    {
                        GameObject.DestroyImmediate(oldReticle);
                    }
                }

                // Instanciar Retícula única
                GameObject reticleInstance = null;
                if (reticlePrefab != null)
                {
                    reticleInstance = (GameObject)PrefabUtility.InstantiatePrefab(reticlePrefab);
                    reticleInstance.name = "PlacementReticle";
                    reticleInstance.SetActive(false);
                }

                var soAR = new SerializedObject(arPlacer);
                soAR.FindProperty("battlefieldPrefab").objectReferenceValue = battlefieldPrefab;
                soAR.FindProperty("raycastManager").objectReferenceValue = xrOriginObj.GetComponent<UnityEngine.XR.ARFoundation.ARRaycastManager>();
                soAR.FindProperty("planeManager").objectReferenceValue = xrOriginObj.GetComponent<UnityEngine.XR.ARFoundation.ARPlaneManager>();
                if (reticleInstance != null) soAR.FindProperty("placementReticle").objectReferenceValue = reticleInstance;
                soAR.ApplyModifiedPropertiesWithoutUndo();

                // Configurar LightEstimationController
                var camObj = xrOriginObj.transform.Find("Camera Offset/Main Camera");
                if (camObj != null)
                {
                    if (camObj.GetComponent<AudioListener>() == null)
                    {
                        camObj.gameObject.AddComponent<AudioListener>();
                    }
                    var camMgr = camObj.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
                    var dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
                    var soLE = new SerializedObject(lightEst);
                    if (camMgr != null) soLE.FindProperty("cameraManager").objectReferenceValue = camMgr;
                    if (dirLight != null) soLE.FindProperty("directionalLight").objectReferenceValue = dirLight;
                    soLE.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 3. Canvas y HUD Completo
            BuildUserInterface(towers);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void ConfigureWaves(WaveSpawner ws, Dictionary<string, GameObject> enemies)
        {
            var soWS = new SerializedObject(ws);
            var wavesProp = soWS.FindProperty("waves");
            wavesProp.arraySize = 4;

            void SetWave(int index, string title, (string enemyKey, int count, float interval)[] groups, string bossKey)
            {
                var waveElem = wavesProp.GetArrayElementAtIndex(index);
                waveElem.FindPropertyRelative("waveTitle").stringValue = title;
                waveElem.FindPropertyRelative("delayBeforeBoss").floatValue = 3.0f;

                if (!string.IsNullOrEmpty(bossKey) && enemies.ContainsKey(bossKey))
                {
                    waveElem.FindPropertyRelative("bossPrefab").objectReferenceValue = enemies[bossKey];
                }

                var groupsProp = waveElem.FindPropertyRelative("enemyGroups");
                groupsProp.arraySize = groups.Length;
                for (int g = 0; g < groups.Length; g++)
                {
                    var groupElem = groupsProp.GetArrayElementAtIndex(g);
                    groupElem.FindPropertyRelative("groupName").stringValue = $"{groups[g].count}x {groups[g].enemyKey}";
                    groupElem.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemies[groups[g].enemyKey];
                    groupElem.FindPropertyRelative("count").intValue = groups[g].count;
                    groupElem.FindPropertyRelative("spawnInterval").floatValue = groups[g].interval;
                }
            }

            // Oleada 1: Introducción (8x Pixel) -> Boss Distorsión
            SetWave(0, "Oleada 1: Ruido Blanco", new[] { ("Enemy_Pixel", 8, 1.2f) }, "Boss_Distortion");

            // Oleada 2: Interferencia (6x Static + 4x Pixel) -> Boss Feedback
            SetWave(1, "Oleada 2: Acople Acústico", new[] { ("Enemy_Static", 6, 1.1f), ("Enemy_Pixel", 4, 0.8f) }, "Boss_Feedback");

            // Oleada 3: Enjambre Masivo (5x Amp + 6x Pixel) -> Boss Reina Glitch
            SetWave(2, "Oleada 3: Corrupción Glitch", new[] { ("Enemy_Amp", 5, 1.3f), ("Enemy_Pixel", 6, 0.7f) }, "Boss_GlitchQueen");

            // Oleada 4: Mezcla Final (8x Static + 6x Amp) -> Boss Mezcla Final
            SetWave(3, "Oleada 4: Concierto del Fin del Mundo", new[] { ("Enemy_Static", 8, 0.9f), ("Enemy_Amp", 6, 1.0f) }, "Boss_FinalMix");

            soWS.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildUserInterface(Dictionary<TowerType, GameObject> towers)
        {
            // Canvas Screen Space
            GameObject canvasObj = GameObject.Find("ConcertDefense_HUD_Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("ConcertDefense_HUD_Canvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.matchWidthOrHeight = 0.5f;

                // Limpiar cualquier elemento previo para asegurar cero duplicados
                for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject.DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
                }
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            // --- 1. HUDController ---
            HUDController hud = canvasObj.GetComponent<HUDController>();
            if (hud == null) hud = canvasObj.AddComponent<HUDController>();

            // Panel de guía AR
            GameObject arGuidance = CreatePanel(canvasObj.transform, "AR_GuidancePanel", new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), new Vector2(0f, 0f), new Vector2(800, 90), new Color(0f, 0f, 0f, 0.75f));
            TextMeshProUGUI arGuideText = CreateText(arGuidance.transform, "GuideText", "Apunta tu cámara a una mesa o piso para detectar planos...", 26, font, Color.white, TextAlignmentOptions.Center);
            arGuideText.rectTransform.sizeDelta = new Vector2(760, 70);

            // Gameplay HUD Root (Contenedor transparente SIN componente Image para cero interferencia de raycasts)
            GameObject gameplayRoot = new GameObject("GameplayHUD_Root");
            gameplayRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform grRT = gameplayRoot.AddComponent<RectTransform>();
            grRT.anchorMin = Vector2.zero;
            grRT.anchorMax = Vector2.one;
            grRT.offsetMin = Vector2.zero;
            grRT.offsetMax = Vector2.zero;

            // Barra Superior de Recursos: se adapta responsive de borde a borde (16:9, móviles o Free Aspect)
            GameObject topBar = CreatePanel(gameplayRoot.transform, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -45f), new Vector2(-40f, 75f), new Color(0.08f, 0.08f, 0.14f, 0.88f));

            // Monedas (alineadas a la izquierda con padding)
            GameObject coinsObj = new GameObject("CoinsText");
            coinsObj.transform.SetParent(topBar.transform, false);
            RectTransform coinsRT = coinsObj.AddComponent<RectTransform>();
            coinsRT.anchorMin = new Vector2(0f, 0.5f);
            coinsRT.anchorMax = new Vector2(0f, 0.5f);
            coinsRT.pivot = new Vector2(0f, 0.5f);
            coinsRT.anchoredPosition = new Vector2(25f, 0f);
            coinsRT.sizeDelta = new Vector2(200f, 50f);
            TextMeshProUGUI coinsText = coinsObj.AddComponent<TextMeshProUGUI>();
            if (font != null) coinsText.font = font;
            coinsText.text = "150 pts";
            coinsText.fontSize = 26;
            coinsText.color = new Color(1f, 0.9f, 0.1f);
            coinsText.alignment = TextAlignmentOptions.MidlineLeft;
            coinsText.raycastTarget = false;

            // Slider de Salud del Escenario (centrado)
            GameObject healthSliderObj = CreateSlider(topBar.transform, "StageHealthSlider", Vector2.zero, new Vector2(340f, 26f), 20f);
            Slider stageSlider = healthSliderObj.GetComponent<Slider>();
            RectTransform sliderRT = healthSliderObj.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRT.pivot = new Vector2(0.5f, 0.5f);
            sliderRT.anchoredPosition = Vector2.zero;
            TextMeshProUGUI healthText = CreateText(healthSliderObj.transform, "HealthText", "20 / 20", 18, font, Color.white, TextAlignmentOptions.Center);
            healthText.raycastTarget = false;

            // Texto de Oleada (anclado a la derecha, NUNCA tapado con padding y sin recorte)
            GameObject waveObj = new GameObject("WaveText");
            waveObj.transform.SetParent(topBar.transform, false);
            RectTransform waveRT = waveObj.AddComponent<RectTransform>();
            waveRT.anchorMin = new Vector2(1f, 0.5f);
            waveRT.anchorMax = new Vector2(1f, 0.5f);
            waveRT.pivot = new Vector2(1f, 0.5f);
            waveRT.anchoredPosition = new Vector2(-25f, 0f);
            waveRT.sizeDelta = new Vector2(220f, 50f);
            TextMeshProUGUI waveText = waveObj.AddComponent<TextMeshProUGUI>();
            if (font != null) waveText.font = font;
            waveText.text = "Oleada 1 / 4";
            waveText.fontSize = 24;
            waveText.color = new Color(0f, 1f, 0.9f);
            waveText.alignment = TextAlignmentOptions.MidlineRight;
            waveText.raycastTarget = false;
            waveText.textWrappingMode = TextWrappingModes.NoWrap;
            waveText.overflowMode = TextOverflowModes.Overflow;

            // Botón Iniciar Oleada (con texto sin raycast para garantizar clic instantáneo)
            GameObject startWaveBtnObj = CreateButton(gameplayRoot.transform, "StartWaveButton", new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(320, 80), new Color(0f, 0.85f, 0.75f));
            TextMeshProUGUI startWaveLabel = CreateText(startWaveBtnObj.transform, "Label", "INICIAR OLEADA", 28, font, Color.black, TextAlignmentOptions.Center);
            startWaveLabel.raycastTarget = false;
            Button startWaveBtn = startWaveBtnObj.GetComponent<Button>();

            // Barra de Jefe (Oculta por defecto)
            GameObject bossBar = CreatePanel(gameplayRoot.transform, "BossBarContainer", new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.92f), Vector2.zero, new Vector2(850, 70), new Color(0.2f, 0.05f, 0.1f, 0.9f));
            TextMeshProUGUI bossTitleText = CreateText(bossBar.transform, "BossNameText", "JEFE: Distorsión", 24, font, new Color(1f, 0.2f, 0.6f), TextAlignmentOptions.Top);
            bossTitleText.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            GameObject bossSliderObj = CreateSlider(bossBar.transform, "BossHealthSlider", new Vector2(0f, -12f), new Vector2(750f, 22f), 600f);
            Slider bossSlider = bossSliderObj.GetComponent<Slider>();
            bossBar.SetActive(false);

            // Fin de Partida: Game Over & Victoria
            GameObject gameOverPanel = CreatePanel(canvasObj.transform, "GameOverPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 420), new Color(0.12f, 0.02f, 0.05f, 0.95f));
            CreateText(gameOverPanel.transform, "Title", "¡CONCIERTO ARRUINADO!\n<size=60%>El Ánimo del escenario ha caído a 0</size>", 36, font, new Color(1f, 0.2f, 0.3f), TextAlignmentOptions.Center);
            GameObject restartGOverBtn = CreateButton(gameOverPanel.transform, "RestartButton", new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(260, 65), new Color(1f, 0.3f, 0.4f));
            CreateText(restartGOverBtn.transform, "Label", "REINTENTAR", 26, font, Color.white, TextAlignmentOptions.Center);
            gameOverPanel.SetActive(false);

            GameObject victoryPanel = CreatePanel(canvasObj.transform, "VictoryPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 420), new Color(0.02f, 0.12f, 0.08f, 0.95f));
            CreateText(victoryPanel.transform, "Title", "¡CONCIERTO SALVADO!\n<size=60%>Todos los Glitches y Jefes han sido purificados</size>", 36, font, new Color(0f, 1f, 0.6f), TextAlignmentOptions.Center);
            GameObject restartVicBtn = CreateButton(victoryPanel.transform, "RestartButton", new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(260, 65), new Color(0f, 0.9f, 0.5f));
            CreateText(restartVicBtn.transform, "Label", "VOLVER A JUGAR", 26, font, Color.black, TextAlignmentOptions.Center);
            victoryPanel.SetActive(false);

            // Hook a HUDController
            var soHUD = new SerializedObject(hud);
            soHUD.FindProperty("arGuidancePanel").objectReferenceValue = arGuidance;
            soHUD.FindProperty("arGuidanceText").objectReferenceValue = arGuideText;
            soHUD.FindProperty("gameplayHudRoot").objectReferenceValue = gameplayRoot;
            soHUD.FindProperty("coinsText").objectReferenceValue = coinsText;
            soHUD.FindProperty("stageHealthText").objectReferenceValue = healthText;
            soHUD.FindProperty("stageHealthSlider").objectReferenceValue = stageSlider;
            soHUD.FindProperty("waveText").objectReferenceValue = waveText;
            soHUD.FindProperty("startWaveButton").objectReferenceValue = startWaveBtn;
            soHUD.FindProperty("bossBarContainer").objectReferenceValue = bossBar;
            soHUD.FindProperty("bossNameText").objectReferenceValue = bossTitleText;
            soHUD.FindProperty("bossHealthSlider").objectReferenceValue = bossSlider;
            soHUD.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
            soHUD.FindProperty("restartGameOverButton").objectReferenceValue = restartGOverBtn.GetComponent<Button>();
            soHUD.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
            soHUD.FindProperty("restartVictoryButton").objectReferenceValue = restartVicBtn.GetComponent<Button>();
            soHUD.ApplyModifiedPropertiesWithoutUndo();

            // --- 2. RhythmInput UI ---
            RhythmInput ri = canvasObj.GetComponent<RhythmInput>();
            if (ri == null) ri = canvasObj.AddComponent<RhythmInput>();

            GameObject beatBtnObj = CreateButton(gameplayRoot.transform, "BeatButton", new Vector2(0.9f, 0.16f), new Vector2(0.9f, 0.16f), Vector2.zero, new Vector2(130, 130), new Color(0.9f, 0.1f, 0.6f));
            CreateText(beatBtnObj.transform, "BeatLabel", "BEAT\n♪", 28, font, Color.white, TextAlignmentOptions.Center);

            GameObject pulseRingObj = CreatePanel(beatBtnObj.transform, "PulseRing", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 160), Color.clear);
            Image ringImg = pulseRingObj.GetComponent<Image>();
            ringImg.color = new Color(0f, 1f, 0.9f, 0.6f);

            TextMeshProUGUI feedbackText = CreateText(gameplayRoot.transform, "RhythmFeedbackText", "", 42, font, Color.cyan, TextAlignmentOptions.Center);
            feedbackText.rectTransform.anchoredPosition = new Vector2(580f, -220f);

            TextMeshProUGUI comboText = CreateText(gameplayRoot.transform, "ComboText", "", 32, font, Color.yellow, TextAlignmentOptions.Center);
            comboText.rectTransform.anchoredPosition = new Vector2(580f, -320f);

            var soRI = new SerializedObject(ri);
            soRI.FindProperty("beatButton").objectReferenceValue = beatBtnObj.GetComponent<Button>();
            soRI.FindProperty("pulseRing").objectReferenceValue = pulseRingObj.GetComponent<RectTransform>();
            soRI.FindProperty("feedbackText").objectReferenceValue = feedbackText;
            soRI.FindProperty("comboText").objectReferenceValue = comboText;
            soRI.ApplyModifiedPropertiesWithoutUndo();

            // --- 3. UltimateController UI ---
            UltimateController uc = GameObject.Find("[GameManagers]").GetComponent<UltimateController>();
            GameObject ultBtnObj = CreateButton(gameplayRoot.transform, "UltimateButton", new Vector2(0.1f, 0.16f), new Vector2(0.1f, 0.16f), Vector2.zero, new Vector2(130, 130), new Color(0.2f, 0.7f, 1f));
            CreateText(ultBtnObj.transform, "UltLabel", "ULTIMATE\n★", 22, font, Color.white, TextAlignmentOptions.Center);
            Image fillImg = ultBtnObj.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;

            var soUC = new SerializedObject(uc);
            soUC.FindProperty("ultimateButton").objectReferenceValue = ultBtnObj.GetComponent<Button>();
            soUC.FindProperty("chargeFillImage").objectReferenceValue = fillImg;
            soUC.ApplyModifiedPropertiesWithoutUndo();

            // --- 4. TowerSelectorUI ---
            TowerSelectorUI ts = canvasObj.GetComponent<TowerSelectorUI>();
            if (ts == null) ts = canvasObj.AddComponent<TowerSelectorUI>();

            GameObject selectorRoot = CreatePanel(canvasObj.transform, "TowerSelector_Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 480), new Color(0.08f, 0.08f, 0.14f, 0.95f));
            CreateText(selectorRoot.transform, "Header", "SELECCIONA UNA CANTANTE DEFENSIVA", 32, font, new Color(0f, 1f, 0.9f), TextAlignmentOptions.Top);

            GameObject closeSelectorBtn = CreateButton(selectorRoot.transform, "CloseButton", new Vector2(0.95f, 0.92f), new Vector2(0.95f, 0.92f), Vector2.zero, new Vector2(45, 45), new Color(0.8f, 0.2f, 0.2f));
            CreateText(closeSelectorBtn.transform, "X", "X", 24, font, Color.white, TextAlignmentOptions.Center);

            var soTS = new SerializedObject(ts);
            soTS.FindProperty("panelRoot").objectReferenceValue = selectorRoot;
            soTS.FindProperty("closeButton").objectReferenceValue = closeSelectorBtn.GetComponent<Button>();

            var cardsProp = soTS.FindProperty("towerCards");
            cardsProp.arraySize = 4;

            (TowerType type, string name, int cost, Color color, float posX)[] cardDefs = {
                (TowerType.Bass, "Bass", 50, new Color(0f, 0.8f, 0.9f), -330f),
                (TowerType.Treble, "Treble", 40, new Color(1f, 0.85f, 0.1f), -110f),
                (TowerType.Echo, "Echo", 60, new Color(0.9f, 0.1f, 0.6f), 110f),
                (TowerType.Drop, "Drop", 80, new Color(0.7f, 0.2f, 1f), 330f)
            };

            for (int i = 0; i < cardDefs.Length; i++)
            {
                var cardData = cardDefs[i];
                GameObject cardObj = CreatePanel(selectorRoot.transform, $"Card_{cardData.name}", new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(cardData.posX, -15f), new Vector2(195, 300), new Color(0.14f, 0.14f, 0.22f));

                CreateText(cardObj.transform, "Title", cardData.name, 26, font, cardData.color, TextAlignmentOptions.Top);

                GameObject buyBtn = CreateButton(cardObj.transform, "BuyButton", new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(160, 50), cardData.color);
                CreateText(buyBtn.transform, "BtnLabel", "CONSTRUIR", 18, font, Color.black, TextAlignmentOptions.Center);

                TextMeshProUGUI costTxt = CreateText(cardObj.transform, "Cost", $"{cardData.cost} pts", 22, font, Color.yellow, TextAlignmentOptions.Bottom);
                costTxt.rectTransform.anchoredPosition = new Vector2(0f, 15f);

                var elem = cardsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("type").enumValueIndex = (int)cardData.type;
                elem.FindPropertyRelative("characterName").stringValue = cardData.name;
                elem.FindPropertyRelative("cost").intValue = cardData.cost;
                elem.FindPropertyRelative("towerPrefab").objectReferenceValue = towers[cardData.type];
                elem.FindPropertyRelative("cardButton").objectReferenceValue = buyBtn.GetComponent<Button>();
                elem.FindPropertyRelative("costText").objectReferenceValue = costTxt;
            }

            soTS.ApplyModifiedPropertiesWithoutUndo();
            selectorRoot.SetActive(false);

            // --- 5. World Space TowerMenu ---
            BuildWorldSpaceTowerMenu(font);
        }

        private static void BuildWorldSpaceTowerMenu(TMP_FontAsset font)
        {
            GameObject menuCanvas = GameObject.Find("WorldSpace_TowerMenu");
            if (menuCanvas == null)
            {
                menuCanvas = new GameObject("WorldSpace_TowerMenu");
                Canvas canvas = menuCanvas.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 320);
                canvas.transform.localScale = Vector3.one * 0.0018f;
                menuCanvas.AddComponent<GraphicRaycaster>();
            }

            UIFollow follow = menuCanvas.GetComponent<UIFollow>();
            if (follow == null) follow = menuCanvas.AddComponent<UIFollow>();

            TowerMenu tm = menuCanvas.GetComponent<TowerMenu>();
            if (tm == null) tm = menuCanvas.AddComponent<TowerMenu>();

            GameObject root = CreatePanel(menuCanvas.transform, "MenuRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.08f, 0.08f, 0.14f, 0.92f));

            TextMeshProUGUI title = CreateText(root.transform, "TowerName", "Bass Tower", 32, font, Color.cyan, TextAlignmentOptions.Top);
            title.rectTransform.anchoredPosition = new Vector2(0f, -25f);

            TextMeshProUGUI level = CreateText(root.transform, "LevelText", "Nivel: 1/3", 26, font, Color.white, TextAlignmentOptions.Center);
            level.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            GameObject upgBtn = CreateButton(root.transform, "UpgradeButton", new Vector2(0.28f, 0.28f), new Vector2(0.28f, 0.28f), Vector2.zero, new Vector2(160, 55), new Color(0f, 0.8f, 0.5f));
            TextMeshProUGUI upgTxt = CreateText(upgBtn.transform, "Label", "Mejorar (60 pts)", 18, font, Color.black, TextAlignmentOptions.Center);

            GameObject sellBtn = CreateButton(root.transform, "SellButton", new Vector2(0.72f, 0.28f), new Vector2(0.72f, 0.28f), Vector2.zero, new Vector2(160, 55), new Color(0.9f, 0.4f, 0.1f));
            TextMeshProUGUI sellTxt = CreateText(sellBtn.transform, "Label", "Vender (+30 pts)", 18, font, Color.black, TextAlignmentOptions.Center);

            GameObject closeBtn = CreateButton(root.transform, "CloseButton", new Vector2(0.93f, 0.92f), new Vector2(0.93f, 0.92f), Vector2.zero, new Vector2(35, 35), Color.red);
            CreateText(closeBtn.transform, "X", "X", 22, font, Color.white, TextAlignmentOptions.Center);

            var soTM = new SerializedObject(tm);
            soTM.FindProperty("menuRoot").objectReferenceValue = root;
            soTM.FindProperty("uiFollow").objectReferenceValue = follow;
            soTM.FindProperty("towerNameText").objectReferenceValue = title;
            soTM.FindProperty("levelText").objectReferenceValue = level;
            soTM.FindProperty("upgradeCostText").objectReferenceValue = upgTxt;
            soTM.FindProperty("sellRefundText").objectReferenceValue = sellTxt;
            soTM.FindProperty("upgradeButton").objectReferenceValue = upgBtn.GetComponent<Button>();
            soTM.FindProperty("sellButton").objectReferenceValue = sellBtn.GetComponent<Button>();
            soTM.FindProperty("closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            soTM.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 minAnchor, Vector2 maxAnchor, Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = minAnchor;
            rt.anchorMax = maxAnchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = obj.AddComponent<Image>();
            img.color = color;
            if (color == Color.clear)
            {
                img.raycastTarget = false;
            }
            return obj;
        }

        private static GameObject CreateButton(Transform parent, string name, Vector2 minAnchor, Vector2 maxAnchor, Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = CreatePanel(parent, name, minAnchor, maxAnchor, pos, size, color);
            obj.AddComponent<Button>();
            return obj;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, TMP_FontAsset font, Color color, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false; // Desactivar raycastTarget en texto para no bloquear clics de botones padre
            return tmp;
        }

        private static GameObject CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, float maxVal)
        {
            GameObject obj = CreatePanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, new Color(0.2f, 0.2f, 0.25f));
            Slider slider = obj.AddComponent<Slider>();

            GameObject fillArea = CreatePanel(obj.transform, "Fill Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            var fillAreaImg = fillArea.GetComponent<Image>();
            if (fillAreaImg != null) fillAreaImg.raycastTarget = false;

            GameObject fill = CreatePanel(fillArea.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 1f, 0.6f));
            var fillImg = fill.GetComponent<Image>();
            if (fillImg != null) fillImg.raycastTarget = false;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.maxValue = maxVal;
            slider.value = maxVal;
            return obj;
        }

        [MenuItem("ConcertDefense/Clean Canvas and Rebuild UI", priority = 2)]
        public static void CleanAndRebuildUI()
        {
            Debug.Log("[ConcertDefense] Limpiando Canvas y reconstruyendo UI responsive...");

            // Eliminar Object Spawner del template AR para evitar que aparezcan cubos azules al hacer clic
            GameObject spawner = GameObject.Find("Object Spawner");
            if (spawner != null)
            {
                GameObject.DestroyImmediate(spawner);
            }

            var towers = LoadTowerPrefabs();
            BuildUserInterface(towers);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#00FF88><b>[ConcertDefense] ¡Canvas limpiado y HUD reconstruido con éxito sin duplicados!</b></color>");
        }

        private static Dictionary<TowerType, GameObject> LoadTowerPrefabs()
        {
            var dict = new Dictionary<TowerType, GameObject>();
            dict[TowerType.Bass] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Towers/Tower_Bass.prefab");
            dict[TowerType.Treble] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Towers/Tower_Treble.prefab");
            dict[TowerType.Echo] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Towers/Tower_Echo.prefab");
            dict[TowerType.Drop] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS_PATH}/Towers/Tower_Drop.prefab");
            return dict;
        }
        #endregion
    }

    /// <summary>
    /// Limpiador automático que detecta si el Canvas tiene elementos duplicados tras una compilación y los repara.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoCanvasCleaner
    {
        static AutoCanvasCleaner()
        {
            EditorApplication.delayCall += CheckAndCleanCanvasDuplicates;
        }

        private static void CheckAndCleanCanvasDuplicates()
        {
            GameObject canvasObj = GameObject.Find("ConcertDefense_HUD_Canvas");
            if (canvasObj == null) return;

            int guidanceCount = 0;
            int gameplayCount = 0;
            foreach (Transform t in canvasObj.transform)
            {
                if (t.name == "AR_GuidancePanel") guidanceCount++;
                if (t.name == "GameplayHUD_Root") gameplayCount++;
            }

            if (guidanceCount > 1 || gameplayCount > 1 || canvasObj.transform.childCount > 10)
            {
                Debug.LogWarning("[ConcertDefense] Se detectaron elementos duplicados en el Canvas. Reconstruyendo automáticamente...");
                GameSetupUtility.CleanAndRebuildUI();
            }
        }
    }
}
