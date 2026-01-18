using System.Collections.Generic;
using Kasizuki;

namespace COM3D2.i18nEx.Core.TranslationExtract.DataSources
{
    /// <summary>
    /// Guest (Kasizuki) ManData 資料源 - 男性客人資料
    /// </summary>
    public class GuestManDataSource : ApiDataSourceBase<ManData.Data>
    {
        public override string Name => "Guest";

        protected override IList<ManData.Data> FetchData()
        {
            return ManData.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(ManData.Data data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] GuestManData ID{data.ID}");

            // Guest name
            yield return new TranslationEntry
            {
                Term = data.drawNameTerm,
                Original = data.drawName
            };

            // Guest profile
            if (!string.IsNullOrEmpty(data.profileText))
            {
                yield return new TranslationEntry
                {
                    Term = data.profileTextTerm,
                    Original = data.profileText
                };
            }

            // Favorite play
            if (!string.IsNullOrEmpty(data.favoritePlayText))
            {
                yield return new TranslationEntry
                {
                    Term = data.favoritePlayTextTerm,
                    Original = data.favoritePlayText
                };
            }
        }
    }

    /// <summary>
    /// Guest (Kasizuki) PlayData 資料源 - 玩法資料
    /// </summary>
    public class GuestPlayDataSource : ApiDataSourceBase<PlayData.Data>
    {
        public override string Name => "Guest";

        protected override IList<PlayData.Data> FetchData()
        {
            return PlayData.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(PlayData.Data data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] GuestPlayData ID{data.ID}");

            // Play title
            yield return new TranslationEntry
            {
                Term = $"SceneKasizukiMainMenu/プレイタイトル/{data.ID}",
                Original = data.drawName
            };

            // Play description
            if (!string.IsNullOrEmpty(data.strDescription))
            {
                yield return new TranslationEntry
                {
                    Term = $"SceneKasizukiMainMenu/プレイ内容/{data.ID}",
                    Original = data.strDescription
                };
            }

            // Play conditions
            if (data.strConditionArray != null)
            {
                foreach (var condition in data.strConditionArray)
                {
                    if (!string.IsNullOrEmpty(condition))
                    {
                        yield return new TranslationEntry
                        {
                            Term = $"SceneKasizukiMainMenu/プレイ条件/{condition}",
                            Original = condition
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Guest (Kasizuki) RoomData 資料源 - 房間資料
    /// </summary>
    public class GuestRoomDataSource : ApiDataSourceBase<RoomData.Data>
    {
        public override string Name => "Guest";

        protected override IList<RoomData.Data> FetchData()
        {
            return RoomData.GetAllDatas(false);
        }

        protected override IEnumerable<TranslationEntry> ProcessItem(RoomData.Data data, int index, int total)
        {
            Core.Logger.LogInfo($"[{Name}] Progress [{index}/{total}] GuestRoomData ID{data.ID}");

            // Room name
            yield return new TranslationEntry
            {
                Term = $"SceneKasizukiMainMenu/部屋名/{data.uniqueName}",
                Original = data.drawName
            };

            // Room description
            if (!string.IsNullOrEmpty(data.explanatoryText))
            {
                yield return new TranslationEntry
                {
                    Term = $"SceneKasizukiMainMenu/部屋説明/{data.uniqueName}",
                    Original = data.explanatoryText
                };
            }
        }
    }
}
