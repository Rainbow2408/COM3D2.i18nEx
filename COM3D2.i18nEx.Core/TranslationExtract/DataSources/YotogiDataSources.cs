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
            Core.Logger.LogInfo($"[{Name}] Getting datas via API");
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
    /// 直接使用 CsvParser 建構 Command 物件，避免資料快取到靜態欄位
    /// </summary>
    public class YotogiCommandDataSource : ITranslationDataSource
    {
        private readonly HashSet<string> commandHash = new HashSet<string>();

        public string Name => "Yotogi Commands";

        public void Initialize()
        {
            commandHash.Clear();
        }

        public IEnumerable<TranslationEntry> GetEntries()
        {
            Core.Logger.LogInfo($"[{Name}] Getting datas via direct CsvParser (no cache)");
            var skill_data_id_list_ = Skill.skill_data_list;

            // 直接開啟 NEI 檔案，不使用 skill.command 屬性
            using (var settingFile = GameUty.FileSystem.FileOpen("yotogi_skill_command_data.nei"))
            using (var statusFile = GameUty.FileSystem.FileOpen("yotogi_skill_command_status.nei"))
            using (var settingCsv = new CsvParser())
            using (var statusCsv = new CsvParser())
            {
                settingCsv.Open(settingFile);
                statusCsv.Open(statusFile);

                int i = 1, i_total = skill_data_id_list_.Length;
                int processedCount = 0;

                foreach (var sk0 in skill_data_id_list_)
                {
                    int j = 1, j_total = sk0.Values.Count;
                    foreach (var skill in sk0.Values)
                    {
                        processedCount++;
                        Core.Logger.LogInfo($"[{Name}] Progress [{i}/{i_total}][{j}/{j_total}] ID{skill.id}");

                        // Note: Cannot yield inside try-catch (CS1626), so collect entries first
                        var entries = new List<TranslationEntry>();
                        try
                        {
                            // 直接建構 Command 物件，不會存入 skill.command_ 快取
                            var command = new Skill.Data.Command(skill, settingCsv, statusCsv);
                            
                            if (command?.data != null)
                            {
                                foreach (var cmdData in command.data)
                                {
                                    if (cmdData?.basic != null && !string.IsNullOrEmpty(cmdData.basic.name))
                                    {
                                        if (!commandHash.Contains(cmdData.basic.name))
                                        {
                                            commandHash.Add(cmdData.basic.name);
                                            entries.Add(new TranslationEntry
                                            {
                                                Term = cmdData.basic.termName,
                                                Desc = string.Empty,
                                                Original = cmdData.basic.name
                                            });
                                        }
                                    }
                                }
                            }
                            
                            // command 是局部變數，方法結束後會自動被 GC 回收
                        }
                        catch (Exception ex)
                        {
                            Core.Logger.LogWarning($"[{Name}] Failed to get commands for skill {skill.name}: {ex.Message}");
                        }

                        foreach (var entry in entries)
                        {
                            yield return entry;
                        }
                        j++;

                        // 每 100 個技能執行一次 GC
                        if (processedCount % 100 == 0)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                    i++;
                }
            }
            // using 結束後 CsvParser 和 FileStream 會被自動關閉和釋放
            GC.Collect();
        }
    }
}
