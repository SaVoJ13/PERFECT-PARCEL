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

        public float BeltSpeed
        {
            get => beltSpeed;
            set => beltSpeed = Mathf.Max(0f, value);
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

            float deltaMove = beltSpeed * Time.deltaTime;
            var activeList = parcelPool.ActiveParcels;

            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                var parcel = activeList[i];
                if (parcel != null && parcel.IsActiveOnBelt)
                {
                    parcel.MoveOnBelt(deltaMove);

                    // Якщо посилка перетнула лінію падіння
                    if (parcel.transform.position.y <= GameConstants.ConveyorDropLineY)
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
