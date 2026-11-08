using System.Collections.Generic;
using UnityEngine;

namespace PerfectParcel.Parcels
{
    /// <summary>
    /// Пул об'єктів для посилок (Object Pooling).
    /// Забезпечує стабільний фреймрейт 60 FPS та виключає GC Allocations під час геймплею.
    /// </summary>
    public class ParcelPool : MonoBehaviour
    {
        [SerializeField] private GameObject parcelPrefab;
        [SerializeField] private int initialPoolSize = 15;

        private readonly Queue<ParcelController> _availableParcels = new Queue<ParcelController>();
        private readonly List<ParcelController> _activeParcels = new List<ParcelController>();

        public IReadOnlyList<ParcelController> ActiveParcels => _activeParcels;

        public void Initialize(GameObject prefab = null, int size = 15)
        {
            if (prefab != null) parcelPrefab = prefab;
            initialPoolSize = size;

            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewParcelInstance();
            }
        }

        private ParcelController CreateNewParcelInstance()
        {
            GameObject obj;
            if (parcelPrefab != null)
            {
                obj = Instantiate(parcelPrefab, transform);
            }
            else
            {
                obj = new GameObject("PooledParcel");
                obj.transform.SetParent(transform, false);
                obj.AddComponent<ParcelController>();
            }

            var controller = obj.GetComponent<ParcelController>();
            controller.Deactivate();
            _availableParcels.Enqueue(controller);
            return controller;
        }

        public ParcelController GetParcel()
        {
            ParcelController controller;
            if (_availableParcels.Count > 0)
            {
                controller = _availableParcels.Dequeue();
            }
            else
            {
                // Динамічне розширення пулу у разі потреби
                controller = CreateNewParcelInstance();
                _availableParcels.Dequeue();
            }

            _activeParcels.Add(controller);
            return controller;
        }

        public void ReturnParcel(ParcelController parcel)
        {
            if (parcel == null) return;

            parcel.Deactivate();
            _activeParcels.Remove(parcel);
            _availableParcels.Enqueue(parcel);
        }

        public void ReturnAll()
        {
            for (int i = _activeParcels.Count - 1; i >= 0; i--)
            {
                var parcel = _activeParcels[i];
                parcel.Deactivate();
                _availableParcels.Enqueue(parcel);
            }
            _activeParcels.Clear();
        }
    }
}
