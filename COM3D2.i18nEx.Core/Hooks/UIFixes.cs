using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using COM3D2.i18nEx.Core.TranslationManagers;
using HarmonyLib;
using I2.Loc;
using Kasizuki;
using MaidStatus;
using Teikokusou;
using UnityEngine;
using UnityEngine.UI;
using wf;
using Yotogis;

namespace COM3D2.i18nEx.Core.Hooks
{
    internal static class UIFixes
    {
        private static Harmony instance;
        private static bool initialized;
        private static readonly Dictionary<string, Font> customFonts = new();

        public static void Initialize()
        {
            if (initialized)
                return;

            instance = Harmony.CreateAndPatchAll(typeof(UIFixes), "horse.coder.i18nex.ui_fixes");

            initialized = true;
        }

        [HarmonyPatch(typeof(CMSystem), "LoadIni")]
        [HarmonyPostfix]
        public static void PostLoadIni()
        {
            if (Configuration.General.FixSubtitleType.Value)
            {
                Configuration.ScriptTranslations.RerouteTranslationsTo.Value = TranslationsReroute.RouteToJapanese;
                Configuration.General.FixSubtitleType.Value = false;
                GameMain.Instance.CMSystem.SubtitleType = SubtitleDisplayManager.DisplayType.Original;
                GameMain.Instance.CMSystem.SaveIni();
                Core.Logger.LogInfo("Fixed game's subtitle type!");
            }
        }

        [HarmonyPatch(typeof(Status), nameof(Status.maxNameLength), MethodType.Getter)]
        [HarmonyPostfix]
        public static void GetMaxNameLength(ref int __result)
        {
            __result = int.MaxValue;
        }

        [HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void OnSetText(Text __instance, string value)
        {
            SetLoc(__instance.gameObject, value);
        }

        [HarmonyPatch(typeof(UILabel), "ProcessAndRequest")]
        [HarmonyPrefix]
        public static void OnProcessRequest(UILabel __instance)
        {
            SetLoc(__instance.gameObject, __instance.text);
        }

        private static void SetLoc(GameObject go, string text)
        {
            var loc = go.GetComponent<Localize>();
            if (loc != null || string.IsNullOrEmpty(text))
                return;

            var term = $"General/{text.Replace(" ", "_")}";
            if (Configuration.I2Translation.VerboseLogging.Value)
                Core.Logger.LogInfo($"Trying to localize with term \"{term}\"");
            loc = go.AddComponent<Localize>();
            loc.SetTerm(term);
        }

        [HarmonyPatch(typeof(Text), "OnEnable")]
        [HarmonyPrefix]
        public static void ChangeUEUIFont(Text __instance)
        {
            __instance.font = SwapFont(__instance.font);
        }

        [HarmonyPatch(typeof(UILabel), "ProcessAndRequest")]
        [HarmonyPrefix]
        public static void ChangeFont(UILabel __instance)
        {
            __instance.trueTypeFont = SwapFont(__instance.trueTypeFont);
        }

        private static Font SwapFont(Font originalFont)
        {
            if (originalFont == null)
                return null;

            var customFont = Configuration.I2Translation.CustomUIFont.Value.Trim();
            if (string.IsNullOrEmpty(customFont) || originalFont.name == customFont)
                return originalFont;

            var fontId = $"{customFont}#{originalFont.fontSize}";
            if (!customFonts.TryGetValue(fontId, out var font))
                font = customFonts[fontId] = Font.CreateDynamicFontFromOSFont(customFont, originalFont.fontSize);
            return font ?? originalFont;
        }

        [HarmonyPatch(typeof(SceneNetorareCheck), "Start")]
        [HarmonyPostfix]
        public static void LocalizeNTRScene(GameObject ___toggleParent)
        {
            Core.Logger.LogInfo("Fixing NTR check scene.");

            void Localize(string item)
            {
                var result = UTY.GetChildObject(___toggleParent, $"{item}/Result"); //.GetComponent<UILabel>();
                var title = UTY.GetChildObject(___toggleParent, $"{item}/Title");   //.GetComponent<UILabel>();

                var resultLoc = result.AddComponent<Localize>();
                resultLoc.SetTerm($"SceneNetorareCheck/{item}_Result");

                var titleLoc = title.AddComponent<Localize>();
                titleLoc.SetTerm($"SceneNetorareCheck/{item}_Title");
            }

            Localize("Toggle_LockUserDraftMaid");
            Localize("Toggle_IsComPlayer");
        }

        [HarmonyPatch(typeof(SystemShortcut), "OnClick_Info")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LocalizeInfoText(IEnumerable<CodeInstruction> instructions)
        {
            var hasText = false;
            foreach (var codeInstruction in instructions)
            {
                if (codeInstruction.opcode == OpCodes.Callvirt && codeInstruction.operand is MethodInfo minfo &&
                    minfo.Name             == "get_SysDlg")
                {
                    hasText = true;
                }
                else if (hasText)
                {
                    hasText = false;
                    var index = -1;
                    if (OpCodes.Ldloc_0.Value        <= codeInstruction.opcode.Value &&
                        codeInstruction.opcode.Value <= OpCodes.Ldloc_3.Value)
                        index = codeInstruction.opcode.Value - OpCodes.Ldloc_0.Value;
                    else if (codeInstruction.opcode == OpCodes.Ldloc_S || codeInstruction.opcode == OpCodes.Ldloc)
                        index = (int)codeInstruction.operand;

                    if (index < 0)
                    {
                        Core.Logger.LogError("Failed to patch info text localization! Please report this!");
                        yield return codeInstruction;
                        continue;
                    }

                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloca, index);
                    yield return Transpilers.EmitDelegate<TranslateInfo>((ref string text) =>
                    {
                        if (LocalizationManager.TryGetTranslation("System/GameInfo_Description", out var tl))
                            text = string.Format(tl, Product.gameTitle, GameUty.GetBuildVersionText(),
                                                 GameUty.GetGameVersionText(), GameUty.GetLegacyGameVersionText());
                    });
                    yield return new CodeInstruction(OpCodes.Call,
                                                     AccessTools.PropertyGetter(
                                                                                typeof(GameMain),
                                                                                nameof(GameMain.Instance)));
                    yield return new CodeInstruction(OpCodes.Callvirt,
                                                     AccessTools.PropertyGetter(
                                                                                typeof(GameMain),
                                                                                nameof(GameMain.SysDlg)));
                }

                yield return codeInstruction;
            }
        }

        [HarmonyPatch(typeof(LocalizeTarget_NGUI_Label), nameof(LocalizeTarget_NGUI_Label.DoLocalize))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FixDoLocalize(IEnumerable<CodeInstruction> instrs)
        {
            var prop = AccessTools.PropertySetter(typeof(UILabel), nameof(UILabel.text));
            foreach (var ins in instrs)
                if (ins.opcode == OpCodes.Callvirt && (MethodInfo)ins.operand == prop)
                    yield return Transpilers.EmitDelegate<Action<UILabel, string>>((label, text) =>
                    {
                        if (!string.IsNullOrEmpty(text))
                            label.text = text;
                    });
                else
                    yield return ins;
        }

        [HarmonyPatch(typeof(UIWFConditionList), nameof(UIWFConditionList.SetTexts), typeof(KeyValuePair<string[], Color>[]), typeof(int))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FixSetTexts(IEnumerable<CodeInstruction> instrs)
        {
            var setActive = AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive));

            var gotBool = false;
            var done = false;
            foreach (var ins in instrs)
            {
                yield return ins;

                if (!done && ins.opcode == OpCodes.Ldc_I4_1)
                    gotBool = true;
                if (!gotBool || ins.opcode != OpCodes.Callvirt || (MethodInfo)ins.operand != setActive)
                    continue;

                gotBool = false;
                done = true;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld,
                                                 AccessTools.Field(typeof(UIWFConditionList), "condition_label_list_"));
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldloc_3);
                yield return
                    Transpilers.EmitDelegate<Action<List<UILabel>, KeyValuePair<string[], Color>[], int>>
                        ((labels, texts, index) =>
                        {
                            var curText = texts[index];
                            var curLabel = labels[index];
                            if (curText.Key.Length == 1)
                                curLabel.text = Utility.GetTermLastWord(curText.Key[0]);
                            else if (curText.Key.Length > 1)
                                curLabel.text = string.Format(Utility.GetTermLastWord(curText.Key[0]),
                                                              curText.Key
                                                                     .Skip(1)
                                                                     .Select(Utility.GetTermLastWord)
                                                                     .Cast<object>().ToArray());
                        });
            }
        }

        [HarmonyPatch(typeof(SkillAcquisitionCondition), nameof(SkillAcquisitionCondition.CreateConditionTextAndStaturResults))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TranspileCreateConditionTextAndStaturResults(IEnumerable<CodeInstruction> instrs)
        {
            var supportMultiLang = AccessTools.PropertyGetter(typeof(Product), nameof(Product.supportMultiLanguage));
            foreach (var ins in instrs)
                if (ins.opcode == OpCodes.Call && (MethodInfo)ins.operand == supportMultiLang)
                {
                    yield return ins;
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return Transpilers.EmitDelegate<Func<SkillAcquisitionCondition, bool>>(sac =>
                        Product.supportMultiLanguage &&
                        LocalizationManager.TryGetTranslation(sac.yotogi_class.termName, out var _));
                }
                else
                {
                    yield return ins;
                }
        }

        [HarmonyPatch(typeof(MaidManagementMain), "OnSelectChara")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FixNpcMaidBanishment(IEnumerable<CodeInstruction> instrs)
        {
            var isJapan = AccessTools.PropertyGetter(typeof(Product), nameof(Product.isJapan));
            foreach (var ins in instrs)
                if (ins.opcode == OpCodes.Call && (MethodInfo)ins.operand == isJapan)
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                else
                    yield return ins;
        }

        // This seems outdated creating duplicated entries in Edit Mode personality dropdown list.
        /*
        [HarmonyPatch(typeof(ProfileCtrl), "Init")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FixProfileCtrlPersonalityDisplay(IEnumerable<CodeInstruction> instrs)
        {
            var termNameProp = AccessTools.PropertyGetter(typeof(Personal.Data), nameof(Personal.Data.termName));
            foreach (var ins in instrs)
                if (ins.opcode == OpCodes.Callvirt && (MethodInfo)ins.operand == termNameProp)
                    yield return Transpilers.EmitDelegate<Func<Personal.Data, string>>(data =>
                    {
                        if (LocalizationManager.TryGetTranslation(data.termName, out var _))
                            return data.termName;
                        return data.drawName;
                    });
                else
                    yield return ins;
        }
        */
        
        

        [HarmonyPatch(typeof(UILabel), nameof(UILabel.SetCurrentSelection))]
        [HarmonyPrefix]
        public static void OnSetCurrentSelection(UILabel __instance)
        {
            if (UIPopupList.current != null)
                __instance.text = UIPopupList.current.value;
        }

        private delegate void TranslateInfo(ref string text);


        // Trophy Small thumbnails fix
        [HarmonyPatch(typeof(TrophyInfo), nameof(TrophyInfo.SetData))]
        [HarmonyPostfix]
        private static void SetData_PostFix(Trophy.Data trophy_data, ref TrophyInfo __instance)
        {
            //Just reuse the JP texture, it's so small that noone is able to ready anything on it anyway.
            if (!string.IsNullOrEmpty(trophy_data.miniCardTextureFileName))
            {
                string texturFileName = trophy_data.miniCardTextureFileName;
                Sprite sprite2D = wf.Utility.CreateTextureSprite(texturFileName);
                __instance.card_sprite_.sprite2D = sprite2D;
            }
        }


        // Trophy Big pictures fix
        [HarmonyPatch(typeof(SceneTrophyCardFade), nameof(SceneTrophyCardFade.CallCard))]
        [HarmonyPrefix]
        private static bool CallCardPreFix(Trophy.Data tropheyData, Vector3 cardWorldPos, ref SceneTrophyCardFade __instance)
        {
            if (__instance.cardSprite.sprite2D != null && __instance.cardSprite.sprite2D.texture != null)
            {
                UnityEngine.Object.DestroyImmediate(__instance.cardSprite.sprite2D.texture);
            }
            __instance.cardSprite.sprite2D = null;
            if (!string.IsNullOrEmpty(tropheyData.cardTextureFileName))
            {
                string texturFileName = tropheyData.cardTextureFileName;

                // This is the part that causes issues, tbh it's useless but as a precaution I leave it here and add a fallback after
                if (Product.supportMultiLanguage)
                {
                    texturFileName = LocalizationManager.GetTranslation(tropheyData.cardTextureFileNameTerm, true, 0, true, false, null, Product.EnumConvert.ToI2LocalizeLanguageName(Product.systemLanguage));
                }
                // The fallback
                if (String.IsNullOrEmpty(texturFileName))
                {
                    texturFileName = tropheyData.cardTextureFileName;
                }

                Sprite sprite = wf.Utility.CreateTextureSprite(texturFileName);
                if (sprite != null)
                {
                    __instance.cardSprite.sprite2D = sprite;
                    __instance.cardSprite.SetDimensions((int)sprite.rect.width, (int)sprite.rect.height);
                }
            }
            __instance.cardSprite.transform.position = cardWorldPos;
            __instance.uiCardPos = cardWorldPos;
            WfFadeJob.Create(null, __instance, __instance.fadeTime, iTween.EaseType.easeInOutSine);

            return false;
        }


        //Honeymoon location texture fix
        //Translation of the textures' names are handled in the get method, because..?
        [HarmonyPatch(typeof(Honeymoon.HoneymoonDatabase.Localtion), nameof(Honeymoon.HoneymoonDatabase.Localtion.iconFileNames), MethodType.Getter)]
        [HarmonyPostfix]
        public static void GetIconFileNames(ref string[] __result)
        {
            for (int i = 0; i < __result.Length; i++)
            {
                if (__result[i].EndsWith("_en.tex"))
                {
                    string jpIcon = __result[i].Replace("_en", "");
                    __result[i] = jpIcon;
                }
            }
        }


        /* //Imperial Villa button fix
        //Not needed anymore as eng textures are now included in the JP release.        
        //Like the above, the use of the _en suffix breaks the texture
        //And since it's in the middle of the daily scene UI creation, better use a transpiler this time.
        [HarmonyPatch(typeof(DailyCtrl), nameof(DailyCtrl.DisplayViewer))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DisplayViewer_Edit(IEnumerable<CodeInstruction> instructions)
        {
            var checkpoint = new CodeMatcher(instructions)
                                 .MatchForward(false,
                                 new CodeMatch(OpCodes.Ldstr, "bunnre_teikokusou_sales")); //easy to match point

            var result = checkpoint.Advance(3)
                                   .Insert(
                                        new CodeInstruction(OpCodes.Pop),
                                        new CodeInstruction(OpCodes.Ldc_I4_0))
                                   .InstructionEnumeration();

            return result;
        }
        */


        //Guest mode fix
        [HarmonyPatch(typeof(AppealData.Data), nameof(AppealData.Data.GetTexture), new Type[] { typeof(Product.Language) })]
        [HarmonyPostfix]
        public static void GetTexturePrefix(ref Texture2D __result, ref AppealData.Data __instance)
        {
            //Just in case the _en texture is indeed here, let's check for it
            if (__result == null)
            {
                // If it's not then use the JP texture instead as done in the GetTexture() overload

                string filename = __instance.texName + ".tex";

                if (GameUty.FileSystem.IsExistentFile(filename))
                {
                    __result = ImportCM.CreateTexture(filename);
                }
                else
                {
                    Core.Logger.LogError($"{filename} couldn't be found!");
                    __result = null;
                }
            }
        }



        //Tutorial gear menu fix
        //Enable the "?" button in the gear menu
        [HarmonyPatch(typeof(uGUITutorialPanel), nameof(uGUITutorialPanel.IsExistTutorial))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> IsExistTutorial_Edit(IEnumerable<CodeInstruction> instructions)
        {
            var checkpoint = new CodeMatcher(instructions)
                                 .MatchForward(false,
                                 new CodeMatch(OpCodes.Ldstr, "tutorial_list")); //easy to match point


            var result = checkpoint.Advance(1)
                                   .RemoveInstructions(7)
                                   .InstructionEnumeration();

            return result;
        }



        //This tells the game to always load the JP tutorial menu (ENG tutorial files are not available by default on a JP game)
        [HarmonyPatch(typeof(uGUITutorialPanel), nameof(uGUITutorialPanel.ReadCSV))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ReadCSV_Edit(IEnumerable<CodeInstruction> instructions)
        {
            var checkpoint = new CodeMatcher(instructions)
                                 .MatchForward(false,
                                 new CodeMatch(OpCodes.Ldstr, "tutorial_list.nei")); //easy to match point

            var result = checkpoint.Start()
                                   .Insert(new CodeInstruction(OpCodes.Ldstr, "tutorial_list.nei"))
                                   .Advance(1)
                                   .RemoveInstructions(8)
                                   .InstructionEnumeration();

            return result;
        }
    }
}
