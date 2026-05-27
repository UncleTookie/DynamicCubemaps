using System;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace DynamicCubemaps
{
    public class CubemapReplacement
    {
        [XmlAttribute("code")]
        public string Code { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlAttribute("file_prefix")]
        public string FilePrefix { get; set; }

        [XmlIgnore]
        public string Dir { get; set; }

        private Cubemap _cubemap;
        private bool _failed;

        public void DestroyCubemap()
        {
            if (_cubemap != null)
            {
                UnityEngine.Object.Destroy(_cubemap);
            }

            _cubemap = null;
            _failed = false;
        }

        public Cubemap GetCubemap()
        {
            if (_failed)
            {
                return null;
            }

            if (_cubemap != null)
            {
                return _cubemap;
            }

            Texture2D texture = null;
            Cubemap cubemap = null;
            try
            {
                if (string.IsNullOrEmpty(FilePrefix))
                {
                    throw new InvalidOperationException("file_prefix is empty");
                }

                var path = Path.Combine(Dir, FilePrefix + "cubemap.png");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("Combined texture file not found", path);
                }

                var bytes = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    throw new InvalidOperationException($"Failed to load image data from {path}");
                }

                if (texture.width % 4 != 0 || texture.height % 3 != 0)
                {
                    throw new InvalidOperationException(
                        $"Combined texture must use a 4x3 cross layout, but decoded size is {texture.width}x{texture.height}");
                }

                var size = texture.width / 4;
                if (size != texture.height / 3)
                {
                    throw new InvalidOperationException(
                        $"Combined texture has non-square faces: width face size {size}, height face size {texture.height / 3}");
                }

                cubemap = new Cubemap(size, TextureFormat.ARGB32, true);
                cubemap.name = $"DynamicCubemaps_{Code}";
                cubemap.wrapMode = TextureWrapMode.Clamp;

                ExtractFaces(texture, cubemap, size);

                cubemap.anisoLevel = 9;
                cubemap.filterMode = FilterMode.Trilinear;
                cubemap.SmoothEdges();
                cubemap.Apply(true, true);

                _cubemap = cubemap;
                return _cubemap;
            }
            catch (Exception e)
            {
                _failed = true;
                Debug.LogError($"DynamicCubemaps: Failed to load cubemap {Code}: {e.Message}");

                if (cubemap != null)
                {
                    UnityEngine.Object.Destroy(cubemap);
                }

                return null;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private void ExtractFaces(Texture2D texture, Cubemap cubemap, int size)
        {
            ExtractFace(texture, cubemap, CubemapFace.PositiveY, size, size * 2, size);
            ExtractFace(texture, cubemap, CubemapFace.NegativeX, 0, size, size);
            ExtractFace(texture, cubemap, CubemapFace.PositiveZ, size, size, size);
            ExtractFace(texture, cubemap, CubemapFace.PositiveX, size * 2, size, size);
            ExtractFace(texture, cubemap, CubemapFace.NegativeZ, size * 3, size, size);
            ExtractFace(texture, cubemap, CubemapFace.NegativeY, size, 0, size);
        }

        private void ExtractFace(Texture2D texture, Cubemap cubemap, CubemapFace face, int startX, int startY, int size)
        {
            Color[] pixels = texture.GetPixels(startX, startY, size, size);
            Color[] flipped = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int src = y * size + x;
                    int dst = (size - 1 - y) * size + x;

                    if (face == CubemapFace.NegativeY)
                    {
                        dst = x * size + (size - 1 - y);
                    }

                    var color = pixels[src];
                    color.a = 1f;
                    flipped[dst] = color;
                }
            }

            cubemap.SetPixels(flipped, face);
        }
    }
}
