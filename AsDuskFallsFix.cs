﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;

namespace AsDuskFallsFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class ASF : BasePlugin
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
            [HarmonyPatch(typeof(InteriorNight.MenuSystem.GraphicsSettingsController), nameof(InteriorNight.MenuSystem.GraphicsSettingsController.GetBestAlternativeResolution))]
            [HarmonyPrefix]
            public static bool CustomResList(InteriorNight.MenuSystem.GraphicsSettingsController __instance, int __result, ref Il2CppSystem.Collections.Generic.List<InteriorNight.INResolution> __0)
            {
                InteriorNight.INResolution customResolution = new InteriorNight.INResolution
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
            [HarmonyPatch(typeof(InteriorNight.CameraAspectRatioFitter), nameof(InteriorNight.CameraAspectRatioFitter.Update))]
            [HarmonyPrefix]
            public static bool StopAspect(InteriorNight.CameraAspectRatioFitter __instance)
            {
                __instance.wantedAspectRatio = (float)Screen.width / Screen.height;

                // Cursor Clamp
                InteriorNight.GlobalSettings.TARGET_ASPECT_RATIO = (float)Screen.width / Screen.height;
                return true;
            }
        }

        [HarmonyPatch]
        public class SettingsPatch
        {
            [HarmonyPatch(typeof(InteriorNight.QualityManager), nameof(InteriorNight.QualityManager.SetResolutionAndDisplayMode))]
            [HarmonyPostfix]
            public static void SettingsChange()
            {
                if (bUncapFPS.Value)
                {
                    //InteriorNight.QualityManager.desiredFrameRate = 999;
                    QualitySettings.vSyncCount = 1;
                }
                if (iAntialiasing.Value > 0)
                {
                    QualitySettings.antiAliasing = iAntialiasing.Value;
                }
                if (iAnisotropicFiltering.Value > 0)
                {
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    Texture.SetGlobalAnisotropicFilteringLimits(iAnisotropicFiltering.Value, iAnisotropicFiltering.Value);
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
        }

        [HarmonyPatch]
        public class IntroSkipPatch
        {

            [HarmonyPatch(typeof(InteriorNight.SplashScreenManager), nameof(InteriorNight.SplashScreenManager.Start))]
            [HarmonyPostfix]
            public static void SkipSplash(InteriorNight.SplashScreenManager __instance)
            {
                InteriorNight.SplashScreenManager.s_splashScreensDone = true;
            }

            [HarmonyPatch(typeof(InteriorNight.SplashScreenManager.SplashScreen), nameof(InteriorNight.SplashScreenManager.SplashScreen.SetupVideo))]
            [HarmonyPrefix]
            public static bool XboxSound(InteriorNight.SplashScreenManager.SplashScreen __instance)
            {
                return false;
            }

        }

    }
}