using System;
using KebabStreet.Services;

namespace KebabStreet.Game
{
    /// <summary>Що має побачити гравець за підсумком завантаження чи запису.</summary>
    public enum SaveNotice
    {
        None = 0,
        ProgressReset,
        UpdateGame,
        StorageUnavailable,
        SaveDisabled,
        SaveFailed,
    }

    public static class SaveNoticePolicy
    {
        /// <summary>Підсумок завантаження до повідомлення. Заборона запису важливіша за скидання.</summary>
        public static SaveNotice FromLoad(SaveLoadResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Source == SaveLoadSource.NewerVersion)
            {
                return SaveNotice.UpdateGame;
            }

            if (result.Source == SaveLoadSource.Unavailable)
            {
                return SaveNotice.StorageUnavailable;
            }

            if (result.ProgressReset)
            {
                return SaveNotice.ProgressReset;
            }

            return result.CanSave ? SaveNotice.None : SaveNotice.SaveDisabled;
        }

        public static SaveNotice FromWrite(SaveWriteResult result)
        {
            switch (result)
            {
                case SaveWriteResult.Saved:
                    return SaveNotice.None;
                case SaveWriteResult.RejectedNewerSave:
                    return SaveNotice.UpdateGame;
                case SaveWriteResult.RejectedAfterUnavailableLoad:
                    return SaveNotice.StorageUnavailable;
                default:
                    return SaveNotice.SaveFailed;
            }
        }

        /// <summary>Ключ тексту для повідомлення; для None - null.</summary>
        public static string KeyOf(SaveNotice notice)
        {
            switch (notice)
            {
                case SaveNotice.ProgressReset:
                    return TextKeys.NoticeProgressReset;
                case SaveNotice.UpdateGame:
                    return TextKeys.NoticeUpdateGame;
                case SaveNotice.StorageUnavailable:
                    return TextKeys.NoticeStorageUnavailable;
                case SaveNotice.SaveDisabled:
                    return TextKeys.NoticeSaveDisabled;
                case SaveNotice.SaveFailed:
                    return TextKeys.NoticeSaveFailed;
                default:
                    return null;
            }
        }
    }
}
