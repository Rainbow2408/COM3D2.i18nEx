using System;
using System.Collections.Generic;
using Yotogis;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// 夜伽技能資料源 - Yotogi Skills
    /// </summary>
    public class YotogiSkillDataSource : ITranslationDataSource
    {
        public string Name => "Yotogi Skills";

        public void Initialize()
        {
            // No initialization needed
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Core.Logger.LogInfo("Getting yotogi skills via API");
            var skill_data_id_list_ = Skill.skill_data_list;

            int i = 1, i_total = skill_data_id_list_.Length;
            foreach (var sk0 in skill_data_id_list_)
            {
                int j = 1, j_total = sk0.Values.Count;
                foreach (var skill in sk0.Values)
                {
                    Core.Logger.LogInfo($"[{Name}] Progress [{i}/{i_total}][{j}/{j_total}] ID{skill.id}");

                    // Skill name
                    yield return new TranslationEntry
                    {
                        Term = skill.termName,  // "YotogiSkillName/{name}"
                        Desc = string.Empty,
                        Original = skill.name
                    };
                    j++;
                }
                i++;
            }
        }
    }

    /// <summary>
    /// 夜伽指令資料源 - Yotogi Commands
    /// </summary>
    public class YotogiCommandDataSource : ITranslationDataSource
    {
        private readonly HashSet<string> commandHash = new HashSet<string>();

        public string Name => "Yotogi Commands";

        public void Initialize()
        {
            // No initialization needed
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Core.Logger.LogInfo("Getting yotogi commands via API");
            var skill_data_id_list_ = Skill.skill_data_list;

            int i = 1, i_total = skill_data_id_list_.Length;
            foreach (var sk0 in skill_data_id_list_)
            {
                int j = 1, j_total = sk0.Values.Count;
                foreach (var skill in sk0.Values)
                {
                    Core.Logger.LogInfo($"[{Name}] Progress [{i}/{i_total}][{j}/{j_total}] ID{skill.id}");

                    // Commands - use skill.command property (lazy-loads NEI files automatically)
                    // Note: Cannot yield inside try-catch (CS1626), so collect entries first
                    var entries = new List<TranslationEntry>();
                    try
                    {
                        var command = skill.command;
                        if (command?.data != null)
                        {
                            foreach (var cmdData in command.data)
                            {
                                if (cmdData?.basic != null && !string.IsNullOrEmpty(cmdData.basic.name))
                                {
                                    // Avoid duplicate command entries
                                    if (!commandHash.Contains(cmdData.basic.name))
                                    {
                                        commandHash.Add(cmdData.basic.name);
                                        entries.Add(new TranslationEntry
                                        {
                                            Term = cmdData.basic.termName,  // "YotogiSkillCommand/{name}"
                                            Desc = string.Empty,
                                            Original = cmdData.basic.name
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Core.Logger.LogWarning($"[{Name}] Failed to get commands for skill {skill.name}: {ex.Message}");
                    }

                    // Yield collected entries outside try-catch block
                    foreach (var entry in entries)
                    {
                        yield return entry;
                    }
                    j++;
                }
                i++;
            }
        }
    }
}
