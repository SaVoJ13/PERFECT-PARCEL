using UnityEngine;
using PerfectParcel.Audio;
using PerfectParcel.Conveyor;
using PerfectParcel.Economy;
using PerfectParcel.Juice;
using PerfectParcel.Localization;
using PerfectParcel.Monetization;
using PerfectParcel.Parcels;
using PerfectParcel.Sorting;
using PerfectParcel.Storage;
using PerfectParcel.UI;

namespace PerfectParcel.Core
{
    /// <summary>
    /// Автоматичний ініціалізатор сцени прототипу.
    /// Дозволяє протестувати гру в Unity Editor без ручного налаштування префабів:
    /// створює всі необхідні компоненти та запускає конвеєр одразу після натискання Play.
    /// </summary>
    public class PrototypeBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // Налаштування камери під портретний режим
            SetupCamera();

            // Створення підсистем локалізації, збереження та монетизації (Phase 2)
            var saveObj = new GameObject("SaveManager");
            saveObj.AddComponent<SaveManager>();

            var locObj = new GameObject("LocalizationManager");
            locObj.AddComponent<LocalizationManager>();

            var dailyRewardObj = new GameObject("DailyRewardManager");
            dailyRewardObj.AddComponent<DailyRewardManager>();

            var umpObj = new GameObject("UmpConsentManager");
            umpObj.AddComponent<UmpConsentManager>();

            var adsObj = new GameObject("AdsManager");
            adsObj.AddComponent<AdsManager>();

            var audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();

            var hapticsObj = new GameObject("HapticFeedbackManager");
            hapticsObj.AddComponent<HapticFeedbackManager>();

            var floatingScoreObj = new GameObject("FloatingScoreManager");
            floatingScoreObj.AddComponent<FloatingScoreManager>();

            var upgradesObj = new GameObject("UpgradesManager");
            upgradesObj.AddComponent<UpgradesManager>();

            var achievementsObj = new GameObject("AchievementsManager");
            achievementsObj.AddComponent<AchievementsManager>();

            var questsObj = new GameObject("DailyQuestsManager");
            questsObj.AddComponent<DailyQuestsManager>();

            // Створення ігрового конвеєра та сортування
            var poolObj = new GameObject("ParcelPool");
            var pool = poolObj.AddComponent<ParcelPool>();

            var beltObj = new GameObject("ConveyorBelt");
            var belt = beltObj.AddComponent<ConveyorBeltController>();

            // Візуал конвеєрної стрічки
            var beltVisual = new GameObject("BeltVisual");
            var beltRenderer = beltVisual.AddComponent<SpriteRenderer>();
            var beltSprite = Resources.Load<Sprite>("Sprites/Conveyor/belt_tile");
            if (beltSprite != null)
            {
                beltRenderer.sprite = beltSprite;
                beltRenderer.drawMode = SpriteDrawMode.Tiled;
                beltRenderer.size = new Vector2(480f, 1920f);
            }
            beltRenderer.sortingOrder = 0;
            beltVisual.transform.position = Vector3.zero;

            var spawnerObj = new GameObject("ParcelSpawner");
            var spawner = spawnerObj.AddComponent<ParcelSpawner>();

            var inputObj = new GameObject("SwipeInputHandler");
            var input = inputObj.AddComponent<SwipeInputHandler>();

            var validatorObj = new GameObject("SortingValidator");
            var validator = validatorObj.AddComponent<SortingValidator>();

            var gateObj = new GameObject("SortingGate");
            var gate = gateObj.AddComponent<SortingGate>();

            var uiObj = new GameObject("UIManager");
            var ui = uiObj.AddComponent<UIManager>();
            ui.Initialize(input);

            var gmObj = new GameObject("GameManager");
            var gm = gmObj.AddComponent<GameManager>();

            // Впровадження залежностей через рефлексію або приватні поля
            SetPrivateField(gm, "beltController", belt);
            SetPrivateField(gm, "parcelPool", pool);
            SetPrivateField(gm, "spawner", spawner);
            SetPrivateField(gm, "inputHandler", input);
            SetPrivateField(gm, "validator", validator);
            SetPrivateField(gm, "sortingGate", gate);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("MainCamera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            cam.orthographic = true;
            cam.orthographicSize = 960f; // Охоплює віртуальні 1920 по висоті
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f); // Глибокий індиго фон

            if (cam.GetComponent<CameraShakeController>() == null)
            {
                cam.gameObject.AddComponent<CameraShakeController>();
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
