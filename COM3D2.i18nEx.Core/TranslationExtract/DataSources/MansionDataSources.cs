using System.Collections.Generic;
using Teikokusou;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// Mansion (Teikokusou) PlayModeRoomData 資料源 - 房間與客人資料
    /// </summary>
    public class MansionRoomDataSource : ApiDataSourceBase<TeikokusouDatabase.PlayModeRoomData>
    {
        public override string Name => "Mansion";

        public override void Initialize()
        {
            TeikokusouDatabase.CreateData();
        }

        protected override IList<TeikokusouDatabase.PlayModeRoomData> FetchData()
        {
            Core.Logger.LogInfo($"[{Name}] Getting play mode room data via API");
            return TeikokusouDatabase.playmodeRoomData;
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(TeikokusouDatabase.PlayModeRoomData data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] PlayModeRoomData ID{data.id}");

            // Room name
            yield return new TranslationEntry
            {
                Term = data.roomNameTerm,
                Original = data.roomName
            };

            // Guest name
            if (!string.IsNullOrEmpty(data.guestName))
            {
                yield return new TranslationEntry
                {
                    Term = data.guestNameTerm,
                    Original = data.guestName
                };
            }

            // Profile comment
            if (!string.IsNullOrEmpty(data.profileComment))
            {
                yield return new TranslationEntry
                {
                    Term = data.profileCommentTerm,
                    Original = data.profileComment
                };
            }
        }
    }

    /// <summary>
    /// Mansion (Teikokusou) EventData 資料源 - 事件資料
    /// </summary>
    public class MansionEventDataSource : ApiDataSourceBase<TeikokusouDatabase.EventData>
    {
        public override string Name => "Mansion";

        public override void Initialize()
        {
            TeikokusouDatabase.CreateData();
        }

        protected override IList<TeikokusouDatabase.EventData> FetchData()
        {
            Core.Logger.LogInfo($"[{Name}] Getting event data via API");
            return TeikokusouDatabase.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(TeikokusouDatabase.EventData data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] EventData ID{data.id}");

            // Info text (conditions)
            if (!string.IsNullOrEmpty(data.infoText))
            {
                yield return new TranslationEntry
                {
                    Term = data.infoTextTerm,
                    Original = data.infoText
                };
            }

            // Choice title
            if (!string.IsNullOrEmpty(data.choiceTitle))
            {
                yield return new TranslationEntry
                {
                    Term = data.choiceTitleTerm,
                    Original = data.choiceTitle
                };
            }
        }
    }
}
