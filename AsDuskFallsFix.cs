using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;

using InteriorNight.MenuSystem;
using InteriorNight;
using System;

namespace AsDuskFallsFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class ADF : BasePlugin
    {
        internal static new ManualLogSource Log;

        public static ConfigEntry<bool> bUltrawideFixes;
        public static ConfigEntry<bool> bCustomResolution;
        public static ConfigEntry<float> fDesiredResolutionX;
        public static ConfigEntry<float> fDesiredResolutionY;
        public static ConfigEntry<bool> bIntroSkip;
        public static ConfigEntry<bool> bUncapFPS;
        public static ConfigEntry<int> iAnisotropicFiltering;
        public static ConfigEntry<int> iAntialiasing;

        public override void Load()
        {
            // Plugin startup logic
            Log = base.Log;
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // Features
            bUltrawideFixes = Config.Bind("Ultrawide UI Fixes",
                                "UltrawideFixes",
                                true,
                                "Set to true to enable ultrawide UI fixes.");

            bIntroSkip = Config.Bind("Intro Skip",
                                "IntroSkip",
                                 true,
                                "Skip intro logos.");

            bUncapFPS = Config.Bind("Uncap Framerate",
                                "UncapFPS",
                                 true,
                                "Set to true to remove 30 FPS cap and enable vsync.");

            // Custom Resolution
            bCustomResolution = Config.Bind("Set Custom Resolution",
                                "CustomResolution",
                                 true,
                                "Set to true to enable the custom resolution below.");

            fDesiredResolutionX = Config.Bind("Set Custom Resolution",
                                "ResolutionWidth",
                                (float)Display.main.systemWidth, // Set default to display width so we don't leave an unsupported resolution as default.
                                "Set desired resolution width.");

            fDesiredResolutionY = Config.Bind("Set Custom Resolution",
                                "ResolutionHeight",
                                (float)Display.main.systemHeight, // Set default to display height so we don't leave an unsupported resolution as default.
                                "Set desired resolution height.");

            // Graphical Settings
            iAnisotropicFiltering = Config.Bind("Graphical Tweaks",
                                "AnisotropicFiltering.Value",
                                (int)16, // Default = unknown?
                                new ConfigDescription("Set Anisotropic Filtering level. 16 is recommended for quality.",
                                new AcceptableValueRange<int>(1, 16)));

            iAntialiasing = Config.Bind("Graphical Tweaks",
                                "Antialiasing.Value",
                                (int)2, // Default = 2
                                new ConfigDescription("Set MSAA Antialiasing level. 8 is recommended for quality.",
                                new AcceptableValueList<int>(0,2,4,8)));


            // Run CustomResolutionPatch
            if (bCustomResolution.Value)
            {
                Harmony.CreateAndPatchAll(typeof(CustomResolutionPatch));
            }

            // Run UltrawidePatch
            if (bUltrawideFixes.Value)
            {
                Harmony.CreateAndPatchAll(typeof(UltrawidePatch));
            }

            // Run IntroSkipPatch
            if (bIntroSkip.Value)
            {
                Harmony.CreateAndPatchAll(typeof(IntroSkipPatch));
            }

            Harmony.CreateAndPatchAll(typeof(SettingsPatch));
        }

        [HarmonyPatch]
        public class CustomResolutionPatch
        {
            // Add custom resolution
            [HarmonyPatch(typeof(GraphicsSettingsController), nameof(GraphicsSettingsController.GetBestAlternativeResolution))]
            [HarmonyPrefix]
            public static bool CustomResList(GraphicsSettingsController __instance, int __result, ref Il2CppSystem.Collections.Generic.List<INResolution> __0)
            {
                INResolution customResolution = new INResolution
                {
                    width = (int)fDesiredResolutionX.Value,
                    height = (int)fDesiredResolutionY.Value,
                    refreshRate = 0 // 0 = use highest available
                };
                __0.Add(customResolution);

                Log.LogInfo($"Custom resolution {fDesiredResolutionX}x{fDesiredResolutionY} added.");
                return true;
            }

            // Update target aspect ratio
            [HarmonyPatch(typeof(CameraAspectRatioFitter), nameof(CameraAspectRatioFitter.Awake))]
            [HarmonyPrefix]
            public static bool StopAspect(CameraAspectRatioFitter __instance)
            {
                __instance.wantedAspectRatio = (float)fDesiredResolutionX.Value / fDesiredResolutionY.Value;
                Log.LogInfo($"AspectRatioFitter wantedAspectRatio set to {__instance.wantedAspectRatio}.");
                return true;
            }

            // Cursor Clamp
            // This is so janky lmao
            [HarmonyPatch(typeof(LocalClient), nameof(LocalClient.ClampCursorPos))]
            [HarmonyPrefix]
            public static bool Prefix(LocalClient __instance)
            {
                GlobalSettings.TARGET_ASPECT_RATIO = (float)Screen.width / Screen.height;
                Log.LogInfo($"Cursor clamp adjusted.");
                return true;
            }
            [HarmonyPatch(typeof(LocalClient), nameof(LocalClient.ClampCursorPos))]
            [HarmonyPostfix]
            public static void Postfix(LocalClient __instance)
            {
                GlobalSettings.TARGET_ASPECT_RATIO = (float)16/9;
            }

        }

        [HarmonyPatch]
        public class SettingsPatch
        {
            [HarmonyPatch(typeof(QualityManager), nameof(QualityManager.SetResolutionAndDisplayMode))]
            [HarmonyPostfix]
            public static void SettingsChange()
            {
                if (bUncapFPS.Value)
                {
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = 500;
                    Log.LogInfo($"Vsync set to {QualitySettings.vSyncCount}. targetFrameRate set to {Application.targetFrameRate}.");
                }

                if (iAntialiasing.Value > 0)
                {
                    QualitySettings.antiAliasing = iAntialiasing.Value;
                    Log.LogInfo($"Antialiasing set to {iAntialiasing.Value}.");
                }
                if (iAnisotropicFiltering.Value > 0)
                {
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    Texture.SetGlobalAnisotropicFilteringLimits(iAnisotropicFiltering.Value, iAnisotropicFiltering.Value);
                    Log.LogInfo($"Anisotropic filtering force enabled. Value = {iAnisotropicFiltering.Value}.");
                }
            }
        }

        [HarmonyPatch]
        public class UltrawidePatch
        {
            public static float DefaultAspectRatio = (float)16 / 9;
            public static float NewAspectRatio = (float)fDesiredResolutionX.Value / fDesiredResolutionY.Value;
            public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;
            public static float AspectDivider = DefaultAspectRatio / NewAspectRatio;

            // Set screen match mode when object has canvasscaler enabled
            [HarmonyPatch(typeof(CanvasScaler), nameof(CanvasScaler.OnEnable))]
            [HarmonyPostfix]
            public static void SetScreenMatchMode(CanvasScaler __instance)
            {
                if (NewAspectRatio > DefaultAspectRatio)
                {
                    __instance.m_ScreenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                }
            }

            // Center main menu
            [HarmonyPatch(typeof(MainMenu), nameof(MainMenu.OnEnable))]
            [HarmonyPostfix]
            public static void FixMainMenu(MainMenu __instance)
            {
                if (NewAspectRatio > DefaultAspectRatio)
                {
                    GameObject MainMenu = GameObject.Find("Managers/MenuManager/MenuRoot");
                    var oldxPos = MainMenu.transform.position.x;
                    var newxPos = -(float)16/9;
                    Vector3 newPos = MainMenu.transform.position;
                    newPos.x = newxPos;
                    MainMenu.transform.position = newPos;
                    Log.LogInfo($"Centered main menu.");
                }
               
            }
        }

        [HarmonyPatch]
        public class IntroSkipPatch
        {

            [HarmonyPatch(typeof(SplashScreenManager), nameof(SplashScreenManager.Start))]
            [HarmonyPostfix]
            public static void SkipSplash(SplashScreenManager __instance)
            {
                SplashScreenManager.s_splashScreensDone = true;
                Log.LogInfo($"Splashscreens skipped.");
            }

            [HarmonyPatch(typeof(SplashScreenManager.SplashScreen), nameof(SplashScreenManager.SplashScreen.SetupVideo))]
            [HarmonyPrefix]
            public static bool SkipSplashVideo(SplashScreenManager.SplashScreen __instance)
            {
                Log.LogInfo($"Startup video skipped.");
                return false;
            }

        }

    }
}
