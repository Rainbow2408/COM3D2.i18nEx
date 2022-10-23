//Important: Patched Product.Language count need to be SAME!!!
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class i18nEx_Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        if (assembly.Name.Name == "Assembly-CSharp")
        {
            var Log = BepInEx.Logging.Logger.CreateLogSource("i18nEx_Patcher_Log");
            TypeDefinition ProLang = null;
            foreach (var type in assembly.MainModule.Types)
            {
                if (type.Name == "Product")
                {
                    Log.LogInfo($"==================\nType:{type.Name}");
                    foreach (var NestedType in type.NestedTypes)
                    {
                        if (NestedType.Name == "Language")
                        {
                            int i = NestedType.Fields.Count - 1; // - value__
                            foreach (string Lang in Enum.GetValues(typeof(Produc_.Language))
                                                        .Cast<Produc_.Language>()
                                                        .Select(x => x.ToString())
                                                        .ToList())
                            {
                                bool included = false;
                                foreach (var Field in NestedType.Fields)
                                {
                                    if (Lang == Field.Name)
                                    {
                                        included = true;
                                        break;
                                    }
                                }
                                if (!included)
                                {
                                    var FD_Language = new FieldDefinition(Lang, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, NestedType) { Constant = i };
                                    NestedType.Fields.Add(FD_Language);
                                    i++;
                                }
                            }
                            Log.LogInfo("Field Name:\tConstant\tAttributes:");
                            foreach (var Field in NestedType.Fields)
                                Log.LogInfo($"{Field.Name}\t{Field.Constant}\t{Field.Attributes}");
                            ProLang = NestedType;
                            break;
                        }
                    }
                }
            }
            foreach (var type in assembly.MainModule.Types)
            {
                if (type.Name == "LocalizationManager" && ProLang != null)
                {
                    Log.LogInfo($"==================\nType:{type.Name}");
                    foreach (var type_Field in type.Fields)
                    {
                        if (type_Field.Name == "ScriptTranslationMark")
                        {
                            var cctor_ScriptTranslationMark = type.Methods.First(c => c.Name == ".cctor");
                            Instruction InsBefore = null;
                            OpCode op_1 = OpCodes.Nop, op_3 = OpCodes.Nop, op_4 = OpCodes.Nop;
                            MethodReference mr_4 = null;
                            foreach (var _Instruction in cctor_ScriptTranslationMark.Body.Instructions)
                            {
                                if (_Instruction.OpCode == OpCodes.Ldloc_0 && _Instruction.Next.Next.Next != null)
                                {
                                    op_1 = _Instruction.OpCode;
                                    op_3 = _Instruction.Next.Next.OpCode;
                                    op_4 = _Instruction.Next.Next.Next.OpCode;
                                    mr_4 = (MethodReference)_Instruction.Next.Next.Next.Operand;
                                    InsBefore = _Instruction.Next.Next.Next.Next;
                                }
                            }
                            int I4 = int.Parse(op_3.Name.Replace("ldc.i4.", "")) + 1;
                            if (InsBefore != null)
                            {
                                var il_cctor = cctor_ScriptTranslationMark.Body.GetILProcessor();
                                List<string> Mask = new List<string> { "j",
                                "e",
                                "sc",
                                "tc",
                                "af",
                                "ar",
                                "eu",
                                "be",
                                "bg",
                                "ca",
                                "cs",
                                "da",
                                "nl",
                                "et",
                                "fo",
                                "fu",
                                "fr",
                                "de",
                                "el",
                                "he",
                                "is",
                                "id",
                                "it",
                                "ko",
                                "lv",
                                "lt",
                                "nn",
                                "pl",
                                "pt",
                                "ro",
                                "ru",
                                "sr",
                                "sk",
                                "sl",
                                "es",
                                "sv",
                                "th",
                                "tr",
                                "uk",
                                "vi",
                                "hu",
                                };
                                for (int i = I4; i < Mask.Count; i++)
                                {
                                    il_cctor.InsertBefore(InsBefore, il_cctor.Create(op_1));
                                    il_cctor.InsertBefore(InsBefore, il_cctor.Create(OpCodes.Ldstr, Mask[i]));
                                    il_cctor.InsertBefore(InsBefore, il_cctor.Create(OpCodes.Ldc_I4, i));
                                    il_cctor.InsertBefore(InsBefore, il_cctor.Create(OpCodes.Callvirt, mr_4));
                                }
                            }
                            foreach (var Instruction in cctor_ScriptTranslationMark.Body.Instructions.ToList())
                            {
                                Log.LogInfo(Instruction);
                            }
                            break;
                        }
                    }
                }
            }
            BepInEx.Logging.Logger.Sources.Remove(Log);
        }
    }
}
public static class Produc_
{
    //UnityEngine Application.systemLanguage Properties
    public enum Language
    {
        // 1.xx included languages
        Japanese,
        English,
        // 2.18 and 3.18 up included languages
        SimplifiedChinese,    //ChineseSimplified,  //Rename by game developer
        TraditionalChinese,   //ChineseTraditional, //Rename by game developer
        // Plugin Add
        Afrikaans,
        Arabic,
        Basque,
        Belarusian,
        Bulgarian,
        Catalan,
        Czech,
        Danish,
        Dutch,
        Estonian,
        Faroese,
        Finnish,
        French,
        German,
        Greek,
        Hebrew,
        Icelandic,
        Indonesian,
        Italian,
        Korean,
        Latvian,
        Lithuanian,
        Norwegian,
        Polish,
        Portuguese,
        Romanian,
        Russian,
        SerboCroatian,
        Slovak,
        Slovenian,
        Spanish,
        Swedish,
        Thai,
        Turkish,
        Ukrainian,
        Vietnamese,
        Hungarian
    };
}