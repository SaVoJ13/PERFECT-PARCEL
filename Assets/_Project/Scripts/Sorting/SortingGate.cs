using System;
using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;
using PerfectParcel.Parcels;

namespace PerfectParcel.Sorting
{
    /// <summary>
    /// Сортувальний шлюз (Decision Gate).
    /// Знаходить найближчу посилку в зоні сортування конвеєра,
    /// приймає команду свайпу та направляє її на валідацію.
    /// </summary>
    public class SortingGate : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParcelPool parcelPool;
        [SerializeField] private SortingValidator validator;
        [SerializeField] private SwipeInputHandler inputHandler;

        public event Action<bool, int> OnActionProcessed; // isCorrect, livesPenalty

        public void Initialize(ParcelPool pool, SortingValidator val, SwipeInputHandler input)
        {
            parcelPool = pool;
            validator = val;
            inputHandler = input;

            if (inputHandler != null)
            {
                inputHandler.OnSwipeDetected += HandleSwipe;
            }
        }

        private void OnDestroy()
        {
            if (inputHandler != null)
            {
                inputHandler.OnSwipeDetected -= HandleSwipe;
            }
        }

        private void HandleSwipe(SortingDirection direction)
        {
            var targetParcel = FindNearestSortableParcel();
            if (targetParcel == null) return;

            // Валідація правильності
            bool isCorrect = validator.ValidateSorting(targetParcel, direction, out int points, out int coins, out int livesPenalty);

            // Анімація вильоту посилки
            targetParcel.SortToDirection(direction);

            OnActionProcessed?.Invoke(isCorrect, livesPenalty);
        }

        /// <summary>
        /// Пошук найнижчої (найближчої до лінії завершення) активної посилки.
        /// </summary>
        private ParcelController FindNearestSortableParcel()
        {
            if (parcelPool == null) return null;

            ParcelController nearest = null;
            float lowestY = float.MaxValue;

            var activeList = parcelPool.ActiveParcels;
            for (int i = 0; i < activeList.Count; i++)
            {
                var parcel = activeList[i];
                if (parcel != null && parcel.IsActiveOnBelt && !parcel.IsBeingSorted)
                {
                    float y = parcel.transform.position.y;
                    // Посилка повинна бути нижче зони спавну і вище лінії падіння
                    if (y < GameConstants.ConveyorSpawnY && y > GameConstants.ConveyorDropLineY)
                    {
                        if (y < lowestY)
                        {
                            lowestY = y;
                            nearest = parcel;
                        }
                    }
                }
            }

            return nearest;
        }
    }
}
