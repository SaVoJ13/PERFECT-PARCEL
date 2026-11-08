#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PerfectParcel.Editor
{
    /// <summary>
    /// Репозиторій не містить .meta-файлів, тому Unity імпортує PNG як звичайні Texture2D,
    /// і Resources.Load&lt;Sprite&gt;() повертає null (гра мовчки падає на процедурні заглушки).
    /// Цей постпроцесор примусово імпортує всі PNG з Assets/_Project/**/Sprites як Sprite.
    /// 1 одиниця світу = 1 піксель (камера ortho 960 = 1920 px висоти), тому PPU = 1.
    /// </summary>
    public class SpriteImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace("\\", "/");
            if (!path.StartsWith("Assets/_Project/") || !path.Contains("/Sprites/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
    }
}
#endif
