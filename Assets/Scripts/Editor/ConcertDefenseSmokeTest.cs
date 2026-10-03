using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ConcertDefense.AR;
using ConcertDefense.Core;
using ConcertDefense.Enemies;
using ConcertDefense.Player;
using ConcertDefense.Rhythm;
using ConcertDefense.Towers;

namespace ConcertDefense.EditorTools
{
    /// <summary>
    /// Prueba automática: entra en Play, coloca el campo, construye torres y juega las oleadas con un "bot".
    /// Solo se activa con la variable de entorno CONCERT_SMOKE=1 (uso por línea de comandos):
    /// Unity -batchmode -projectPath . -executeMethod ConcertDefense.EditorTools.ConcertDefenseSmokeTest.Run
    /// </summary>
    [InitializeOnLoad]
    public static class ConcertDefenseSmokeTest
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly bool Enabled = Environment.GetEnvironmentVariable("CONCERT_SMOKE") == "1";
        private static readonly string OutDir = Environment.GetEnvironmentVariable("CONCERT_SMOKE_OUT") ?? "Logs";

        private static readonly List<string> report = new List<string>();
        private static readonly List<string> errors = new List<string>();

        private static int step;
        private static double bootTime;
        private static float nextActionTime;
        private static float lastShotTime;
        private static bool beatHooked;
        private static int perfects;
        private static int ultimatesUsed;
        private static int towersBuilt;
        private static int upgrades;
        private static int teleports;
        private static int shotIndex;

        static ConcertDefenseSmokeTest()
        {
            if (!Enabled) return;

            bootTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // Fallos internos del indexador de búsqueda del editor: no son del juego
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search")) return;
                if (condition != null && condition.Contains("UnityEditor.Search")) return;

                errors.Add($"{type}: {condition}\n{stackTrace}");
            }
        }

        private static void Log(string line)
        {
            report.Add($"[t={Time.time:0.0}] {line}");
            Debug.Log("[Smoke] " + line);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - bootTime > 240) Finish("TIMEOUT esperando el modo Play", 3);
                return;
            }

            try
            {
                Drive();
            }
            catch (Exception e)
            {
                errors.Add("Excepción en la prueba: " + e);
                Finish("EXCEPCION", 4);
            }
        }

        private static void Drive()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            switch (step)
            {
                case 0: // Estado inicial del HUD, antes de colocar el campo
                    if (Time.unscaledTime < 2.5f) return;
                    BeatClock.ForceGameTime = true;
                    ReportHud("HUD al empezar a jugar");
                    Log($"Estado inicial: {gm.CurrentState}, sesión AR: {UnityEngine.XR.ARFoundation.ARSession.state}, modo sin AR: {ARPlacementController.Instance != null && ARPlacementController.Instance.IsFallbackMode}");
                    Screenshot("01_inicio");
                    step = 1;
                    break;

                case 1: // El botón Iniciar oleada coloca el campo
                    ClickButton("StartWaveButton");
                    step = 2;
                    nextActionTime = Time.unscaledTime + 1f;
                    break;

                case 2:
                    if (Time.unscaledTime < nextActionTime) return;
                    Log($"Tras pulsar Iniciar oleada: estado {gm.CurrentState}, campo: {(Battlefield.Instance != null ? "colocado" : "NO colocado")}, reloj de ritmo: {BeatClock.IsRunning}");
                    if (!gm.IsPlaying || Battlefield.Instance == null)
                    {
                        Finish("El campo no se colocó", 5);
                        return;
                    }

                    // Avatar: caminar y teletransportarse
                    AvatarController avatar = AvatarController.Instance;
                    Vector3 before = avatar.transform.position;
                    TeleportPad pad = Battlefield.Instance.GetComponentInChildren<TeleportPad>();
                    pad.HandleTap();
                    Log($"Teletransporte: {before} -> {avatar.transform.position} (pad en {pad.transform.position})");

                    // Sin monedas de sobra: Bass + Treble + Echo = 150
                    foreach (BuildSpot spot in Battlefield.Instance.GetComponentsInChildren<BuildSpot>()) spot.RequireAvatarNearby = false;
                    Build("Bass", 2);
                    Build("Treble", 1);
                    Build("Echo", 4);
                    Log($"Torres construidas: {Tower.All.Count}, monedas restantes: {gm.CurrentCoins}");
                    Screenshot("02_campo_colocado");

                    Time.timeScale = 4f;
                    if (!beatHooked)
                    {
                        beatHooked = true;
                        BeatClock.OnBeat += OnBeat;
                    }
                    step = 3;
                    break;

                case 3: // Bucle de partida
                    PlayBot(gm);
                    break;
            }
        }

        private static void OnBeat(int beat)
        {
            // El bot toca justo en el tiempo: debe dar Perfect
            if (RhythmInput.Instance == null) return;
            int before = RhythmInput.Instance.CurrentCombo;
            RhythmInput.Instance.OnBeatPressed();
            if (RhythmInput.Instance.CurrentCombo > before) perfects++;
        }

        private static void PlayBot(GameManager gm)
        {
            if (gm.CurrentState == GameState.Victory || gm.CurrentState == GameState.GameOver)
            {
                ReportHud("HUD al terminar");
                Screenshot("04_final");
                Finish($"RESULTADO: {gm.CurrentState}", 0);
                return;
            }

            if (Time.time > 1500f)
            {
                Finish("TIMEOUT de partida", 6);
                return;
            }

            WaveSpawner ws = WaveSpawner.Instance;

            // Captura a mitad de combate
            if (ws.IsWaveInProgress && Time.time - lastShotTime > 45f && shotIndex < 6)
            {
                lastShotTime = Time.time;
                shotIndex++;
                Screenshot($"03_combate_{shotIndex}");
            }

            if (Time.time < nextActionTime) return;
            nextActionTime = Time.time + 0.5f;

            // Ultimate en cuanto esté listo y haya enemigos
            UltimateController ult = UltimateController.Instance;
            if (ult.IsReady && Enemy.All.Count > 0)
            {
                ult.TriggerUltimate();
                ultimatesUsed++;
            }

            // Afinar torres silenciadas teletransportando a la heroína
            Tower silenced = Tower.All.FirstOrDefault(t => t.IsSilenced);
            if (silenced != null && AvatarController.Instance != null)
            {
                AvatarController.Instance.Teleport(silenced.transform.position + Vector3.right * 0.05f);
                teleports++;
            }

            // Gastar monedas: primero llenar plataformas, luego mejorar
            BuildSpot free = Battlefield.Instance.GetComponentsInChildren<BuildSpot>().FirstOrDefault(s => !s.IsOccupied);
            if (free != null)
            {
                string pick = towersBuilt % 3 == 0 ? "Drop" : (towersBuilt % 3 == 1 ? "Bass" : "Treble");
                TryBuildOn(free, pick);
            }
            else
            {
                Tower upgradable = Tower.All.Where(t => t.CurrentLevel < Tower.MaxLevel).OrderBy(t => t.CurrentUpgradeCost).FirstOrDefault();
                if (upgradable != null && gm.CurrentCoins >= upgradable.CurrentUpgradeCost && upgradable.TryUpgrade()) upgrades++;
            }

            if (!ws.IsWaveInProgress)
            {
                Log($"Oleada {ws.CurrentWaveIndex + 1}: inicio. Ánimo {gm.CurrentHealth}/{gm.MaxHealth}, monedas {gm.CurrentCoins}, torres {Tower.All.Count}");
                ClickButton("StartWaveButton");
                if (!ws.IsWaveInProgress) Log("AVISO: el botón no inició la oleada");
            }
        }

        private static void Build(string towerName, int spotIndex)
        {
            BuildSpot[] spots = Battlefield.Instance.GetComponentsInChildren<BuildSpot>();
            TryBuildOn(spots[spotIndex], towerName);
        }

        private static void TryBuildOn(BuildSpot spot, string towerName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Towers/Tower_{towerName}.prefab");
            if (prefab == null)
            {
                errors.Add("Falta el prefab de torre " + towerName);
                return;
            }

            if (GameManager.Instance.CurrentCoins < prefab.GetComponent<Tower>().BaseCost) return;
            if (spot.BuildTower(prefab)) towersBuilt++;
        }

        private static void ClickButton(string name)
        {
            Button button = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b => b.name == name && b.gameObject.scene.IsValid());
            if (button == null)
            {
                errors.Add("No existe el botón " + name);
                return;
            }

            if (!button.gameObject.activeInHierarchy) Log($"AVISO: el botón {name} está oculto");
            if (button.interactable) button.onClick.Invoke();
        }

        private static void ReportHud(string title)
        {
            GameObject canvas = GameObject.Find("ConcertDefense_HUD_Canvas");
            if (canvas == null)
            {
                errors.Add("No existe el Canvas del HUD");
                return;
            }

            var visible = new List<string>();
            var hidden = new List<string>();
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "GameplayHUD_Root")
                {
                    foreach (Transform item in child)
                    {
                        (item.gameObject.activeInHierarchy ? visible : hidden).Add(item.name);
                    }
                }
                else
                {
                    (child.gameObject.activeInHierarchy ? visible : hidden).Add(child.name);
                }
            }

            Log($"{title} -> VISIBLES: {string.Join(", ", visible)} | OCULTOS: {string.Join(", ", hidden)}");
        }

        /// <summary>Renderiza la cámara principal con el HUD a un PNG.</summary>
        private static void Screenshot(string name)
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Canvas hud = GameObject.Find("ConcertDefense_HUD_Canvas").GetComponent<Canvas>();
                var rt = new RenderTexture(1280, 720, 24);
                RenderTexture previousTarget = cam.targetTexture;

                cam.targetTexture = rt;
                hud.renderMode = RenderMode.ScreenSpaceCamera;
                hud.worldCamera = cam;
                hud.planeDistance = 0.12f;
                Canvas.ForceUpdateCanvases();

                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                hud.renderMode = RenderMode.ScreenSpaceOverlay;
                cam.targetTexture = previousTarget;
                Canvas.ForceUpdateCanvases();

                Directory.CreateDirectory(OutDir);
                File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
            }
            catch (Exception e)
            {
                Log("Captura fallida: " + e.Message);
            }
        }

        private static void Finish(string result, int exitCode)
        {
            EditorApplication.update -= Tick;

            GameManager gm = GameManager.Instance;
            report.Add(result);
            if (gm != null)
            {
                report.Add($"Oleada alcanzada: {gm.CurrentWave}/{gm.TotalWaves}, Ánimo: {gm.CurrentHealth}/{gm.MaxHealth}, monedas: {gm.CurrentCoins}");
            }
            report.Add($"Torres: {Tower.All.Count} (construidas {towersBuilt}, mejoras {upgrades}), Perfects: {perfects}, Ultimates: {ultimatesUsed}, teletransportes para afinar: {teleports}");
            report.Add($"Errores registrados: {errors.Count}");
            report.AddRange(errors.Take(25));

            Directory.CreateDirectory(OutDir);
            File.WriteAllLines(Path.Combine(OutDir, "smoke_result.txt"), report);
            EditorApplication.Exit(errors.Count > 0 && exitCode == 0 ? 1 : exitCode);
        }
    }
}
