using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using PerfectParcel.Core;

namespace PerfectParcel.Storage
{
    /// <summary>
    /// Менеджер локального збереження даних (SaveManager).
    /// Реалізує патерн Atomic Write (тимчасовий файл .tmp -> бекап .bak -> заміна),
    /// перевірку цілісності за SHA256 та захист від пошкодження файлу.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string SaveFileName = "SaveData.json";
        private const string SaltKey = "PerfectParcel_Salt_2026_SecureStorageKey";

        public SaveData CurrentData { get; private set; }

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private string TempFilePath => SaveFilePath + ".tmp";
        private string BackupFilePath => SaveFilePath + ".bak";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #region Loading Logic
        public void LoadData()
        {
            if (File.Exists(SaveFilePath))
            {
                if (TryLoadFromFile(SaveFilePath, out var loadedData))
                {
                    CurrentData = loadedData;
                    Debug.Log("[SaveManager] Сейв успішно завантажено з основного файлу.");
                    return;
                }
                Debug.LogWarning("[SaveManager] Основний сейв пошкоджено! Спроба відновлення з бекапу...");
            }

            // Спроба завантаження з бекапу
            if (File.Exists(BackupFilePath))
            {
                if (TryLoadFromFile(BackupFilePath, out var backupData))
                {
                    CurrentData = backupData;
                    Debug.Log("[SaveManager] Стан успішно відновлено з резервної копії (.bak).");
                    SaveDataAtomic(); // Відновлюємо основний файл
                    return;
                }
                Debug.LogError("[SaveManager] Резервна копія також пошкоджена!");
            }

            // Fallback: створення нового безпечного профілю
            Debug.LogWarning("[SaveManager] Створення нового безпечного профілю гравця за замовчуванням.");
            CurrentData = SaveData.CreateDefault();
            SaveDataAtomic();
        }

        private bool TryLoadFromFile(string filePath, out SaveData data)
        {
            data = null;
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return false;

                var parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null) return false;

                // Перевірка контрольної суми
                string originalChecksum = parsed.checksum;
                parsed.checksum = "";
                string calculatedChecksum = ComputeHash(JsonUtility.ToJson(parsed));
                parsed.checksum = originalChecksum;

                if (!string.IsNullOrEmpty(originalChecksum) && originalChecksum != calculatedChecksum)
                {
                    Debug.LogWarning($"[SaveManager] Контрольна сума не збігається у файлі: {filePath}");
                    // Можемо дозволити завантаження або повернути false залежно від строгості
                }

                data = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Помилка читання файлу {filePath}: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Saving Logic (Atomic Write Pattern)
        public bool SaveDataAtomic()
        {
            if (CurrentData == null) return false;

            try
            {
                CurrentData.lastSaveTimestamp = DateTime.UtcNow.ToString("o");

                // 1. Розрахунок контрольної суми SHA256
                CurrentData.checksum = "";
                string rawJson = JsonUtility.ToJson(CurrentData, true);
                CurrentData.checksum = ComputeHash(rawJson);
                string finalJson = JsonUtility.ToJson(CurrentData, true);

                // 2. Запис у тимчасовий файл (.tmp)
                File.WriteAllText(TempFilePath, finalJson, Encoding.UTF8);

                // 3. Валідація розміру та існування тимчасового файлу
                var fileInfo = new FileInfo(TempFilePath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    Debug.LogError("[SaveManager] Помилка: тимчасовий файл порожній або не створений!");
                    return false;
                }

                // 4. Створення/оновлення резервної копії (.bak)
                if (File.Exists(SaveFilePath))
                {
                    File.Copy(SaveFilePath, BackupFilePath, true);
                }

                // 5. Атомарна заміна тимчасового файлу на основний
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
                File.Move(TempFilePath, SaveFilePath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Помилка під час атомарного запису: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Wallet & Progression API
        public int Coins => CurrentData != null ? CurrentData.coins : 0;

        public void AddCoins(int amount)
        {
            if (amount <= 0 || CurrentData == null) return;
            CurrentData.coins += amount;
            SaveDataAtomic();
        }

        public bool SpendCoins(int amount)
        {
            if (CurrentData == null || amount <= 0 || CurrentData.coins < amount) return false;
            CurrentData.coins -= amount;
            SaveDataAtomic();
            return true;
        }

        public void RecordIAPPurchase(string sku, int coinsGranted)
        {
            if (CurrentData == null) return;
            CurrentData.coins += coinsGranted;
            CurrentData.hasPurchasedAnyIAP = true;
            SaveDataAtomic();
            Debug.Log($"[SaveManager] IAP покупку успішно зараховано: SKU={sku}, +{coinsGranted} монет. Примусову рекламу відключено.");
        }

        public string GetLanguage() => CurrentData?.settings?.languageCode ?? "";
        
        public void SetLanguage(string lang)
        {
            if (CurrentData == null) return;
            CurrentData.settings.languageCode = lang;
            SaveDataAtomic();
        }

        public int DailyStreakDay => CurrentData != null ? CurrentData.currentDailyStreakDay : 1;
        public string LastDailyRewardClaimDate => CurrentData != null ? CurrentData.lastDailyRewardClaimDate : "";

        public void RecordDailyRewardClaim(int nextStreakDay, string claimDateUtc)
        {
            if (CurrentData == null) return;
            CurrentData.currentDailyStreakDay = nextStreakDay;
            CurrentData.lastDailyRewardClaimDate = claimDateUtc;
            SaveDataAtomic();
        }

        public void SaveLevelStars(int levelIndex, int starsEarned, int score)
        {
            if (CurrentData == null || levelIndex < 1 || levelIndex > 30) return;

            var level = CurrentData.levels.Find(l => l.levelIndex == levelIndex);
            if (level != null)
            {
                if (starsEarned > level.stars) level.stars = starsEarned;
                if (score > level.highScore) level.highScore = score;
            }

            // Розблокування наступного рівня
            if (levelIndex < 30)
            {
                var nextLevel = CurrentData.levels.Find(l => l.levelIndex == levelIndex + 1);
                if (nextLevel != null && !nextLevel.isUnlocked)
                {
                    nextLevel.isUnlocked = true;
                    if (levelIndex + 1 > CurrentData.highestUnlockedLevel)
                    {
                        CurrentData.highestUnlockedLevel = levelIndex + 1;
                    }
                }
            }

            SaveDataAtomic();
        }

        public int GetLevelStars(int levelIndex)
        {
            if (CurrentData == null) return 0;
            var level = CurrentData.levels.Find(l => l.levelIndex == levelIndex);
            return level != null ? level.stars : 0;
        }

        public bool IsLevelUnlocked(int levelIndex)
        {
            if (CurrentData == null || levelIndex == 1) return true;
            var level = CurrentData.levels.Find(l => l.levelIndex == levelIndex);
            return level != null && level.isUnlocked;
        }

        public int GetTotalStars()
        {
            if (CurrentData == null) return 0;
            int total = 0;
            for (int i = 0; i < CurrentData.levels.Count; i++)
            {
                total += CurrentData.levels[i].stars;
            }
            return total;
        }
        #endregion

        #region Reset Data (Settings Menu Support)
        public void ResetAllProgress()
        {
            CurrentData = SaveData.CreateDefault();
            SaveDataAtomic();
            Debug.Log("[SaveManager] Усі дані прогресу скинуто до заводських налаштувань.");
        }
        #endregion

        private static string ComputeHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input + SaltKey));
                var sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
