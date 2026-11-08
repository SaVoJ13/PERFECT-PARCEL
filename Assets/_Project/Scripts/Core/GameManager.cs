using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Levels;
using PerfectParcel.Parcels;
using PerfectParcel.Sorting;

namespace PerfectParcel.Core
{
    /// <summary>
    /// Головний менеджер життєвого циклу гри (GameManager).
    /// Контролює зміну станів (Playing, Paused, LevelComplete, GameOver),
    /// облік життів, умов перемоги, завантаження конфігурацій рівнів та запуск систем.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("System References")]
        [SerializeField] private ConveyorBeltController beltController;
        [SerializeField] private ParcelPool parcelPool;
        [SerializeField] private ParcelSpawner spawner;
        [SerializeField] private SwipeInputHandler inputHandler;
        [SerializeField] private SortingValidator validator;
        [SerializeField] private SortingGate sortingGate;

        [Header("Runtime State")]
        [SerializeField] private GameState currentState = GameState.None;
        [SerializeField] private int currentLevelIndex = 1;
        [SerializeField] private int currentLives = GameConstants.DefaultStartingLives;
        [SerializeField] private int targetParcels = GameConstants.TargetParcelsLevel1;

        public GameState CurrentState => currentState;
        public int CurrentLevelIndex => currentLevelIndex;
        public int CurrentLives => currentLives;
        public int TargetParcels => targetParcels;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            InitializeSystems();
            StartLevel(1);
        }

        public void InitializeSystems()
        {
            // Ініціалізація пулу
            if (parcelPool != null) parcelPool.Initialize(null, 15);

            // Зв'язування систем
            if (beltController != null && parcelPool != null) beltController.Initialize(parcelPool);
            if (spawner != null && parcelPool != null) spawner.Initialize(parcelPool);
            if (sortingGate != null) sortingGate.Initialize(parcelPool, validator, inputHandler);

            // Підписка на події
            if (beltController != null) beltController.OnParcelDropped += HandleParcelDropped;
            if (sortingGate != null) sortingGate.OnActionProcessed += HandleSortingAction;
        }

        private void OnDestroy()
        {
            if (beltController != null) beltController.OnParcelDropped -= HandleParcelDropped;
            if (sortingGate != null) sortingGate.OnActionProcessed -= HandleSortingAction;
        }

        public void StartLevel(int levelIndex)
        {
            currentLevelIndex = Mathf.Clamp(levelIndex, 1, 30);
            var config = LevelConfigLoader.GetLevelConfig(currentLevelIndex);

            if (config != null)
            {
                targetParcels = config.targetParcels;
                currentLives = config.maxLives;

                if (beltController != null) beltController.BeltSpeed = config.conveyorSpeed;
            }
            else
            {
                targetParcels = GameConstants.TargetParcelsLevel1;
                currentLives = GameConstants.DefaultStartingLives;
                if (beltController != null) beltController.BeltSpeed = GameConstants.DefaultConveyorSpeed;
            }

            if (validator != null) validator.ResetStats();
            if (beltController != null) beltController.ResetBelt();

            SetState(GameState.Playing);

            if (beltController != null) beltController.ResumeBelt();
            if (spawner != null)
            {
                float interval = config != null ? config.spawnInterval : GameConstants.DefaultSpawnInterval;
                spawner.StartSpawning(interval, config);
            }

            PublishStatusEvents();
        }

        public void StartGameSession(int targetCount)
        {
            StartLevel(currentLevelIndex);
        }

        private void HandleSortingAction(bool isCorrect, int livesPenalty)
        {
            if (currentState != GameState.Playing) return;

            if (!isCorrect)
            {
                DeductLives(livesPenalty);
            }
            else
            {
                // Перевірка умови завершення рівня
                if (validator != null && validator.SortedParcelsCount >= targetParcels)
                {
                    HandleLevelComplete();
                }
            }

            PublishStatusEvents();
        }

        private void HandleParcelDropped(ParcelController parcel)
        {
            if (currentState != GameState.Playing) return;

            // Пропущена посилка списує 1 життя
            DeductLives(1);
            EventBus.Publish(new ParcelMissedEvent
            {
                ParcelId = parcel != null && parcel.Data != null ? parcel.Data.Id : 0,
                RemainingLives = currentLives
            });

            PublishStatusEvents();
        }

        private void DeductLives(int amount)
        {
            currentLives = Mathf.Max(0, currentLives - amount);

            EventBus.Publish(new LivesChangedEvent
            {
                CurrentLives = currentLives,
                MaxLives = GameConstants.DefaultStartingLives
            });

            if (currentLives <= 0)
            {
                HandleGameOver();
            }
        }

        private void HandleLevelComplete()
        {
            SetState(GameState.LevelComplete);
            if (spawner != null) spawner.StopSpawning();
            if (beltController != null) beltController.StopBelt();
        }

        private void HandleGameOver()
        {
            SetState(GameState.GameOver);
            if (spawner != null) spawner.StopSpawning();
            if (beltController != null) beltController.StopBelt();
        }

        public void SetState(GameState newState)
        {
            if (currentState == newState) return;
            var oldState = currentState;
            currentState = newState;

            EventBus.Publish(new GameStateChangedEvent
            {
                PreviousState = oldState,
                NewState = newState
            });
        }

        private void PublishStatusEvents()
        {
            EventBus.Publish(new ScoreChangedEvent
            {
                CurrentScore = (validator != null) ? validator.TotalScore : 0,
                CurrentCoins = (validator != null) ? validator.TotalCoins : 0,
                SortedCount = (validator != null) ? validator.SortedParcelsCount : 0,
                TargetCount = targetParcels
            });
        }
    }
}
