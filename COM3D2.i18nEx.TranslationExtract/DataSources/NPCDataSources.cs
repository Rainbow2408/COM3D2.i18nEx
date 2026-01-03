using System.Collections.Generic;
using BepInEx.Logging;
using SceneNPCEdit;

namespace TranslationExtract.DataSources
{
    /// <summary>
    /// NPC 資料源 - Main NPC Data
    /// </summary>
    public class NPCDataSource : ApiDataSource<EditCharacterDatabase.Data>
    {
        public override string Name => "NPC";

        protected override IList<EditCharacterDatabase.Data> FetchData()
        {
            Logger?.LogInfo("Getting NPC data");
            return EditCharacterDatabase.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(EditCharacterDatabase.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] ID{data.id}");

            // NPC first name (名前) and last name (苗字)
            if (data.npcData != null)
            {
                yield return new TranslationEntry
                {
                    Term = data.firstNameTerm,
                    Desc = string.Empty,
                    Original = data.npcData.firstName
                };

                yield return new TranslationEntry
                {
                    Term = data.lastNameTerm,
                    Desc = string.Empty,
                    Original = data.npcData.lastName
                };
            }

            // NPC description (説明)
            if (!string.IsNullOrEmpty(data.additionalInformationText))
            {
                yield return new TranslationEntry
                {
                    Term = data.informationTerm,
                    Desc = string.Empty,
                    Original = data.additionalInformationText
                };
            }
        }
    }

    /// <summary>
    /// SubMaid 資料源 - Sub Maid Data
    /// </summary>
    public class SubMaidDataSource : ApiDataSource<SubMaid.Data>
    {
        public override string Name => "SubMaid";

        protected override IList<SubMaid.Data> FetchData()
        {
            Logger?.LogInfo("Getting SubMaid data via API");
            return SubMaid.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(SubMaid.Data data, int index, int total)
        {
            Logger?.LogDebug($"[{Name}] Progress [{index}/{total}] ID{data.id}");

            // Process status (normal side)
            if (data.status != null)
            {
                // Last name (苗字)
                if (!string.IsNullOrEmpty(data.status.lastName))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.status.lastNameTerm,
                        Desc = string.Empty,
                        Original = data.status.lastName
                    };
                }

                // First name (名前)
                if (!string.IsNullOrEmpty(data.status.firstName))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.status.firstNameTerm,
                        Desc = string.Empty,
                        Original = data.status.firstName
                    };
                }

                // Personal text (性格)
                if (!string.IsNullOrEmpty(data.status.personalText))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.status.personalTextTerm,
                        Desc = string.Empty,
                        Original = data.status.personalText
                    };
                }

                // Relation text (状態)
                if (!string.IsNullOrEmpty(data.status.relationText))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.status.relationTextTerm,
                        Desc = string.Empty,
                        Original = data.status.relationText
                    };
                }
            }

            // Process secondStatus (kiss side) if different
            if (data.secondStatus != null)
            {
                // Last name (苗字) - Kiss Side
                if (!string.IsNullOrEmpty(data.secondStatus.lastName))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.secondStatus.lastNameTerm,
                        Desc = string.Empty,
                        Original = data.secondStatus.lastName
                    };
                }

                // First name (名前) - Kiss Side
                if (!string.IsNullOrEmpty(data.secondStatus.firstName))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.secondStatus.firstNameTerm,
                        Desc = string.Empty,
                        Original = data.secondStatus.firstName
                    };
                }

                // Personal text (性格) - Kiss Side
                if (!string.IsNullOrEmpty(data.secondStatus.personalText))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.secondStatus.personalTextTerm,
                        Desc = string.Empty,
                        Original = data.secondStatus.personalText
                    };
                }

                // Relation text (状態) - Kiss Side
                if (!string.IsNullOrEmpty(data.secondStatus.relationText))
                {
                    yield return new TranslationEntry
                    {
                        Term = data.secondStatus.relationTextTerm,
                        Desc = string.Empty,
                        Original = data.secondStatus.relationText
                    };
                }
            }
        }
    }
}
