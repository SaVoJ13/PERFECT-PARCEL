using System.Collections;
using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;
using PerfectParcel.Levels;

namespace PerfectParcel.Parcels
{
    /// <summary>
    /// Спавнер посилок на конвеєрі.
    /// Керує інтервалами появи, генерацією типу посилки згідно з LevelConfig
    /// та випадковим вибором сортувального напрямку.
    /// </summary>
    public class ParcelSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float spawnInterval = GameConstants.DefaultSpawnInterval;
        [SerializeField] private bool autoSpawn = false;

        [Header("References")]
        [SerializeField] private ParcelPool parcelPool;

        private Coroutine _spawnRoutine;
        private int _nextParcelId = 1;
        private LevelConfig _currentLevelConfig;

        public void Initialize(ParcelPool pool)
        {
            parcelPool = pool;
        }

        public void StartSpawning(float interval = -1f, LevelConfig levelConfig = null)
        {
            if (interval > 0f) spawnInterval = interval;
            _currentLevelConfig = levelConfig;

            StopSpawning();
            autoSpawn = true;
            _spawnRoutine = StartCoroutine(SpawnLoop());
        }

        public void StopSpawning()
        {
            autoSpawn = false;
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        private IEnumerator SpawnLoop()
        {
            while (autoSpawn)
            {
                SpawnNextParcel();
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        public ParcelController SpawnNextParcel()
        {
            ParcelType type = ParcelType.Standard;
            if (_currentLevelConfig != null)
            {
                type = _currentLevelConfig.RollParcelType();
            }

            return SpawnSingleParcel(type);
        }

        public ParcelController SpawnSingleParcel(ParcelType type = ParcelType.Standard)
        {
            if (parcelPool == null) return null;

            var parcel = parcelPool.GetParcel();
            if (parcel == null) return null;

            // Випадковий напрямок сортування (Ліво / Прямо / Право)
            var direction = (SortingDirection)Random.Range(0, 3);

            var data = new ParcelData
            {
                Id = _nextParcelId++,
                Type = type,
                TargetDirection = direction,
                SpeedModifier = (type == ParcelType.Express) ? 1.4f : (type == ParcelType.Heavy) ? 0.7f : 1.0f,
                BasePoints = GameConstants.BaseStandardPoints,
                BaseCoins = GameConstants.BaseStandardCoins,
                IsScanned = (type != ParcelType.Secured)
            };

            var spawnPos = new Vector3(0f, GameConstants.ConveyorSpawnY, 0f);
            parcel.Initialize(data, spawnPos);
            return parcel;
        }
    }
}
