using System;
using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;

namespace PerfectParcel.Parcels
{
    /// <summary>
    /// Контролер індивідуальної посилки на конвеєрі.
    /// Відповідає за позицію, візуальну реакцію на свайп та взаємодію з гравцем.
    /// </summary>
    public class ParcelController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer tagRenderer;
        [SerializeField] private BoxCollider2D boxCollider;

        public ParcelData Data { get; private set; }
        public bool IsActiveOnBelt { get; private set; }
        public bool IsBeingSorted { get; private set; }

        public event Action<ParcelController> OnDropLinePassed;
        public event Action<ParcelController, SortingDirection> OnSorted;

        private Vector3 _targetExitPosition;
        private float _exitAnimationProgress;
        private const float ExitAnimationDuration = 0.25f;

        private void Awake()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(1.8f, 1.8f);

            // Якщо спрайт-рендерер не призначений у префабі, створюємо процедурний
            if (bodyRenderer == null)
            {
                var bodyObj = new GameObject("ParcelBody");
                bodyObj.transform.SetParent(transform, false);
                bodyRenderer = bodyObj.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = CreateBoxSprite();
                bodyRenderer.sortingOrder = 10;
            }

            if (tagRenderer == null)
            {
                var tagObj = new GameObject("DirectionTag");
                tagObj.transform.SetParent(transform, false);
                tagRenderer = tagObj.AddComponent<SpriteRenderer>();
                tagRenderer.sprite = CreateTagSprite();
                tagRenderer.sortingOrder = 11;
                tagRenderer.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            }
        }

        public void Initialize(ParcelData data, Vector3 spawnPosition)
        {
            Data = data;
            transform.position = spawnPosition;
            transform.localScale = Vector3.one;
            IsActiveOnBelt = true;
            IsBeingSorted = false;
            gameObject.SetActive(true);

            ApplyVisuals();
        }

        private static bool _spritesLoaded = false;
        private static Sprite _spriteStandard;
        private static Sprite _spriteExpress;
        private static Sprite _spriteFragile;
        private static Sprite _spriteHeavy;
        private static Sprite _spriteSecured;
        private static Sprite _chuteLeft;
        private static Sprite _chuteStraight;
        private static Sprite _chuteRight;

        private static void LoadSpritesIfNeeded()
        {
            if (_spritesLoaded) return;
            _spritesLoaded = true;

            _spriteStandard = Resources.Load<Sprite>("Sprites/Parcels/parcel_standard");
            _spriteExpress = Resources.Load<Sprite>("Sprites/Parcels/parcel_express");
            _spriteFragile = Resources.Load<Sprite>("Sprites/Parcels/parcel_fragile");
            _spriteHeavy = Resources.Load<Sprite>("Sprites/Parcels/parcel_heavy");
            _spriteSecured = Resources.Load<Sprite>("Sprites/Parcels/parcel_secured");

            _chuteLeft = Resources.Load<Sprite>("Sprites/Conveyor/chute_left");
            _chuteStraight = Resources.Load<Sprite>("Sprites/Conveyor/chute_straight");
            _chuteRight = Resources.Load<Sprite>("Sprites/Conveyor/chute_right");
        }

        private void ApplyVisuals()
        {
            LoadSpritesIfNeeded();

            Sprite targetBodySprite = null;
            switch (Data.Type)
            {
                case ParcelType.Standard: targetBodySprite = _spriteStandard; break;
                case ParcelType.Express: targetBodySprite = _spriteExpress; break;
                case ParcelType.Fragile: targetBodySprite = _spriteFragile; break;
                case ParcelType.Heavy: targetBodySprite = _spriteHeavy; break;
                case ParcelType.Secured: targetBodySprite = _spriteSecured; break;
            }

            if (bodyRenderer != null)
            {
                if (targetBodySprite != null)
                {
                    bodyRenderer.sprite = targetBodySprite;
                    bodyRenderer.color = Color.white;
                }
                else
                {
                    // Базовий колір корпусу залежно від типу (Fallback)
                    switch (Data.Type)
                    {
                        case ParcelType.Standard:
                            bodyRenderer.color = new Color(0.85f, 0.47f, 0.02f); // Крафтовий картон
                            break;
                        case ParcelType.Express:
                            bodyRenderer.color = new Color(0.98f, 0.57f, 0.24f); // Яскравий помаранчевий
                            break;
                        case ParcelType.Fragile:
                            bodyRenderer.color = new Color(0.95f, 0.96f, 0.98f); // Білий порцеляновий
                            break;
                        case ParcelType.Heavy:
                            bodyRenderer.color = new Color(0.47f, 0.21f, 0.06f); // Темний дубовий
                            break;
                        case ParcelType.Secured:
                            bodyRenderer.color = new Color(0.58f, 0.64f, 0.72f); // Сріблясто-сталевий
                            break;
                    }
                }
            }

            if (tagRenderer != null)
            {
                Sprite targetTagSprite = null;
                switch (Data.TargetDirection)
                {
                    case SortingDirection.Left: targetTagSprite = _chuteLeft; break;
                    case SortingDirection.Straight: targetTagSprite = _chuteStraight; break;
                    case SortingDirection.Right: targetTagSprite = _chuteRight; break;
                }

                if (targetTagSprite != null)
                {
                    tagRenderer.sprite = targetTagSprite;
                    tagRenderer.color = Color.white;
                }
                else
                {
                    // Колір мітки доставки (Синій / Зелений / Червоний) (Fallback)
                    tagRenderer.color = Data.DirectionColor;
                }
            }
        }

        public void MoveOnBelt(float deltaY)
        {
            if (!IsActiveOnBelt || IsBeingSorted) return;

            transform.position += new Vector3(0f, -deltaY, 0f);

            // Перевірка лінії падіння
            if (transform.position.y <= GameConstants.ConveyorDropLineY)
            {
                IsActiveOnBelt = false;
                OnDropLinePassed?.Invoke(this);
            }
        }

        public void SortToDirection(SortingDirection direction)
        {
            if (!IsActiveOnBelt || IsBeingSorted) return;

            IsBeingSorted = true;
            IsActiveOnBelt = false;

            // Визначаємо вектор вильоту в напрямний жолоб
            float exitX = 0f;
            switch (direction)
            {
                case SortingDirection.Left:
                    exitX = -600f;
                    break;
                case SortingDirection.Straight:
                    exitX = 0f;
                    break;
                case SortingDirection.Right:
                    exitX = 600f;
                    break;
            }

            _targetExitPosition = new Vector3(exitX, transform.position.y - 200f, transform.position.z);
            _exitAnimationProgress = 0f;

            OnSorted?.Invoke(this, direction);
        }

        private void Update()
        {
            if (IsBeingSorted)
            {
                _exitAnimationProgress += Time.deltaTime / ExitAnimationDuration;
                transform.position = Vector3.Lerp(transform.position, _targetExitPosition, _exitAnimationProgress);
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, _exitAnimationProgress);

                if (_exitAnimationProgress >= 1f)
                {
                    IsBeingSorted = false;
                    gameObject.SetActive(false);
                }
            }
        }

        public void Deactivate()
        {
            IsActiveOnBelt = false;
            IsBeingSorted = false;
            gameObject.SetActive(false);
        }

        #region Procedural Sprites Fallback (No External Asset Dependency)
        private static Sprite CreateBoxSprite()
        {
            var texture = new Texture2D(64, 64);
            var pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32);
        }

        private static Sprite CreateTagSprite()
        {
            var texture = new Texture2D(32, 32);
            var pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
        }
        #endregion
    }
}
