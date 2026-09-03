using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CharacterEditor_FacialAnimation_EyePatch
{
    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        static ModStartup()
        {
            try
            {
                var harmony = new Harmony("neachi.charactereditor.facialanimation.eyepatch");

                Type headToolType = AccessTools.TypeByName("CharacterEditor.HeadTool");
                Type presetPawnType = AccessTools.TypeByName("CharacterEditor.PresetPawn");

                if (headToolType != null)
                {
                    MethodInfo setEyeColorMethod = AccessTools.Method(headToolType, "SetEyeColor", new[] { typeof(Pawn), typeof(Color) });
                    if (setEyeColorMethod != null)
                    {
                        harmony.Patch(setEyeColorMethod, prefix: new HarmonyMethod(typeof(EyeColorPatches), nameof(EyeColorPatches.SetEyeColor_Prefix)));
                    }

                    MethodInfo setEyeColor2Method = AccessTools.Method(headToolType, "SetEyeColor2", new[] { typeof(Pawn), typeof(Color) });
                    if (setEyeColor2Method != null)
                    {
                        harmony.Patch(setEyeColor2Method, prefix: new HarmonyMethod(typeof(EyeColorPatches), nameof(EyeColorPatches.SetEyeColor2_Prefix)));
                    }
                }

                if (presetPawnType != null)
                {
                    MethodInfo generatePawnMethod = AccessTools.Method(presetPawnType, "GeneratePawn", new[] { typeof(bool), typeof(bool) })
                        ?? AccessTools.Method(presetPawnType, "GeneratePawn");
                    if (generatePawnMethod != null)
                    {
                        harmony.Patch(generatePawnMethod, postfix: new HarmonyMethod(typeof(EyeColorPatches), nameof(EyeColorPatches.GeneratePawn_Postfix)));
                    }

                    MethodInfo postProcessMethod = AccessTools.Method(presetPawnType, "PostProcess");
                    if (postProcessMethod != null)
                    {
                        harmony.Patch(postProcessMethod, postfix: new HarmonyMethod(typeof(EyeColorPatches), nameof(EyeColorPatches.PostProcess_Postfix)));
                    }
                }

                Log.Message("[Character Editor - Facial Animation EyePatch] Initialized and applied patches successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[Character Editor - Facial Animation EyePatch] Failed to initialize: {ex}");
            }
        }
    }

    public static class EyeColorPatches
    {
        private static readonly Type cEditorType = AccessTools.TypeByName("CharacterEditor.CEditor");
        private static readonly Type facialToolType = AccessTools.TypeByName("CharacterEditor.FacialTool");
        private static readonly MethodInfo isFacialAnimationActiveGetter = AccessTools.PropertyGetter(cEditorType, "IsFacialAnimationActive");
        private static readonly PropertyInfo apiProperty = AccessTools.Property(cEditorType, "API");
        private static readonly MethodInfo updateGraphicsMethod = AccessTools.Method(cEditorType, "UpdateGraphics");
        private static readonly MethodInfo faGetControllerCompMethod = AccessTools.Method(facialToolType, "FA_GetControllerComp", new[] { typeof(Pawn), typeof(string), typeof(bool) });

        private static bool IsFacialAnimationActive
        {
            get
            {
                if (isFacialAnimationActiveGetter != null)
                {
                    return (bool)isFacialAnimationActiveGetter.Invoke(null, null);
                }
                return ModsConfig.IsActive("Nals.FacialAnimation") || ModLister.HasActiveModWithName("[NL] Facial Animation - WIP");
            }
        }

        private static ThingComp GetEyeballControllerComp(Pawn pawn)
        {
            if (pawn == null) return null;
            if (faGetControllerCompMethod != null)
            {
                return (ThingComp)faGetControllerCompMethod.Invoke(null, new object[] { pawn, "EyeballControllerComp", true });
            }
            var comps = (pawn as ThingWithComps)?.AllComps;
            if (comps != null)
            {
                for (int i = 0; i < comps.Count; i++)
                {
                    var c = comps[i];
                    if (c != null && c.GetType().Name.Contains("EyeballControllerComp"))
                    {
                        return c;
                    }
                }
            }
            return null;
        }

        private static void UpdateGraphics(Pawn pawn = null)
        {
            try
            {
                if (apiProperty != null && updateGraphicsMethod != null)
                {
                    object apiInstance = apiProperty.GetValue(null, null);
                    if (apiInstance != null)
                    {
                        updateGraphicsMethod.Invoke(apiInstance, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CE-FA EyePatch] Failed to update CEditor graphics: {ex.Message}");
            }

            try
            {
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            }
            catch { }
        }

        public static Color ParseCEColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.clear;

            string[] array = hex.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length >= 4)
            {
                try
                {
                    float r = Convert.ToInt32(array[0], 16) / 255f;
                    float g = Convert.ToInt32(array[1], 16) / 255f;
                    float b = Convert.ToInt32(array[2], 16) / 255f;
                    float a = Convert.ToInt32(array[3], 16) / 255f;
                    return new Color(r, g, b, a);
                }
                catch { }
            }

            string cleanHex = hex.Replace("-", "").TrimStart('#');
            if (ColorUtility.TryParseHtmlString("#" + cleanHex, out Color parsed))
            {
                return parsed;
            }

            return Color.clear;
        }

        public static bool SetEyeColor_Prefix(Pawn p, Color col)
        {
            if (p == null || !IsFacialAnimationActive) return true;

            try
            {
                ThingComp comp = GetEyeballControllerComp(p);
                if (comp != null)
                {
                    var tr = Traverse.Create(comp);
                    tr.Property("FaceColor")?.SetValue(col);
                    tr.Field("color")?.SetValue(col);
                    tr.Field("dirtyFlag")?.SetValue(true);

                    UpdateGraphics(p);
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[CE-FA EyePatch] Error in SetEyeColor_Prefix: {e}");
            }
            return true;
        }

        public static bool SetEyeColor2_Prefix(Pawn p, Color col)
        {
            if (p == null || !IsFacialAnimationActive) return true;

            try
            {
                ThingComp comp = GetEyeballControllerComp(p);
                if (comp != null)
                {
                    var tr = Traverse.Create(comp);
                    tr.Property("FaceSecondColor")?.SetValue(col);
                    tr.Field("secondColor")?.SetValue(col);
                    tr.Field("dirtyFlag")?.SetValue(true);

                    UpdateGraphics(p);
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[CE-FA EyePatch] Error in SetEyeColor2_Prefix: {e}");
            }
            return true;
        }

        public static void GeneratePawn_Postfix(object __instance)
        {
            try
            {
                RestoreEyeColors(__instance);
            }
            catch (Exception e)
            {
                Log.Error($"[CE-FA EyePatch] Error in GeneratePawn_Postfix: {e}");
            }
        }

        public static void PostProcess_Postfix(object __instance)
        {
            try
            {
                RestoreEyeColors(__instance);
            }
            catch (Exception e)
            {
                Log.Error($"[CE-FA EyePatch] Error in PostProcess_Postfix: {e}");
            }
        }

        private static void RestoreEyeColors(object presetInstance)
        {
            if (presetInstance == null || !IsFacialAnimationActive) return;

            var tr = Traverse.Create(presetInstance);
            Pawn pawn = tr.Field("pawn").GetValue<Pawn>();
            if (pawn == null) return;

            IDictionary dicParams = tr.Field("dicParams").GetValue<IDictionary>();
            if (dicParams == null) return;

            Color col1 = Color.clear;
            Color col2 = Color.clear;

            foreach (DictionaryEntry entry in dicParams)
            {
                string keyStr = entry.Key?.ToString();
                string valStr = entry.Value?.ToString();

                if (string.IsNullOrEmpty(valStr)) continue;

                int keyInt = -1;
                if (entry.Key is int i) keyInt = i;
                else if (entry.Key != null && entry.Key.GetType().IsEnum) keyInt = Convert.ToInt32(entry.Key);

                if (keyInt == 19 || keyStr == "P19_eyecolor" || keyStr == "19")
                {
                    col1 = ParseCEColor(valStr);
                }
                else if (keyInt == 53 || keyStr == "P53_eyecolor2" || keyStr == "53")
                {
                    col2 = ParseCEColor(valStr);
                }
            }

            if (col1 != Color.clear || col2 != Color.clear)
            {
                ThingComp comp = GetEyeballControllerComp(pawn);
                if (comp != null)
                {
                    var compTr = Traverse.Create(comp);
                    if (col1 != Color.clear)
                    {
                        compTr.Property("FaceColor")?.SetValue(col1);
                        compTr.Field("color")?.SetValue(col1);
                    }
                    if (col2 != Color.clear)
                    {
                        compTr.Property("FaceSecondColor")?.SetValue(col2);
                        compTr.Field("secondColor")?.SetValue(col2);
                    }
                    compTr.Field("dirtyFlag")?.SetValue(true);

                    UpdateGraphics(pawn);
                }
            }
        }
    }
}