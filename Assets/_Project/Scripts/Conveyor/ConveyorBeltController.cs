using System;
using UnityEngine;
using PerfectParcel.Core;
using PerfectParcel.Parcels;

namespace PerfectParcel.Conveyor
{
    /// <summary>
    /// Контролер руху конвеєрної стрічки.
    /// Відповідає за переміщення всіх активних посилок зверху вниз,
    /// контроль швидкості та виявлення пропущених вантажів на лінії падіння.
    /// </summary>
    public class ConveyorBeltController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float beltSpeed = GameConstants.DefaultConveyorSpeed;
        [SerializeField] private bool isRunning = true;

        [Header("References")]
        [SerializeField] private ParcelPool parcelPool;

        public event Action<ParcelController> OnParcelDropped;

        /// <summary>Поточна лінія падіння (з урахуванням апгрейду Safety Buffer) — спільна для стрічки та шлюзу.</summary>
        public static float CurrentDropLineY { get; private set; } = GameConstants.ConveyorDropLineY;

        private float _dropLineGraceSeconds;

        /// <summary>Додаткові секунди перед падінням посилки (Safety Buffer).</summary>
        public float DropLineGraceSeconds
        {
            get => _dropLineGraceSeconds;
            set { _dropLineGraceSeconds = Mathf.Max(0f, value); RecalculateDropLine(); }
        }

        public float BeltSpeed
        {
            get => beltSpeed;
            set { beltSpeed = Mathf.Max(0f, value); RecalculateDropLine(); }
        }

        private void RecalculateDropLine()
        {
            CurrentDropLineY = GameConstants.ConveyorDropLineY - beltSpeed * _dropLineGraceSeconds;
        }

        private void OnDestroy()
        {
            CurrentDropLineY = GameConstants.ConveyorDropLineY;
        }

        public bool IsRunning
        {
            get => isRunning;
            set => isRunning = value;
        }

        public void Initialize(ParcelPool pool)
        {
            parcelPool = pool;
        }

        private void Update()
        {
            if (!isRunning || parcelPool == null) return;

            float baseMove = beltSpeed * Time.deltaTime;
            float dropLineY = CurrentDropLineY;
            var activeList = parcelPool.ActiveParcels;

            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                var parcel = activeList[i];
                if (parcel != null && parcel.IsActiveOnBelt)
                {
                    // Express +40%, Heavy -30% (ParcelData.SpeedModifier)
                    float modifier = parcel.Data != null ? parcel.Data.SpeedModifier : 1f;
                    parcel.MoveOnBelt(baseMove * modifier);

                    // Якщо посилка перетнула лінію падіння
                    if (parcel.transform.position.y <= dropLineY)
                    {
                        HandleParcelDrop(parcel);
                    }
                }
            }
        }

        private void HandleParcelDrop(ParcelController parcel)
        {
            OnParcelDropped?.Invoke(parcel);
            parcelPool.ReturnParcel(parcel);
        }

        public void StopBelt()
        {
            isRunning = false;
        }

        public void ResumeBelt()
        {
            isRunning = true;
        }

        public void ResetBelt()
        {
            isRunning = false;
            if (parcelPool != null)
            {
                parcelPool.ReturnAll();
            }
        }
    }
}
