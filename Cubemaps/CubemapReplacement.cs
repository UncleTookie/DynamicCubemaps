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

        [XmlAttribute("timePeriod")]
        public string TimePeriod { get; set; }

        [XmlAttribute("isNight")]
        public bool IsNight { get; set; }

        [XmlAttribute("is_night")]
        public bool IsNightLegacy
        {
            get { return IsNight; }
            set { IsNight = value; }
        }

        [XmlAttribute("isSunset")]
        public bool IsSunset { get; set; }

        [XmlAttribute("is_sunset")]
        public bool IsSunsetLegacy
        {
            get { return IsSunset; }
            set { IsSunset = value; }
        }

        [XmlAttribute("isSunrise")]
        public bool IsSunrise { get; set; }

        [XmlAttribute("is_sunrise")]
        public bool IsSunriseLegacy
        {
            get { return IsSunrise; }
            set { IsSunrise = value; }
        }

        [XmlAttribute("isDay")]
        public bool IsDay { get; set; }

        [XmlAttribute("is_day")]
        public bool IsDayLegacy
        {
            get { return IsDay; }
            set { IsDay = value; }
        }

        [XmlAttribute("combined_texture")]
        public string CombinedTexture { get; set; }

        [XmlAttribute("file_prefix")]
        public string FilePrefix { get; set; }

        [XmlAttribute("is_split_format")]
        public bool IsSplitFormat { get; set; }

        [XmlIgnore]
        public string Directory { get; set; }

        private Cubemap cubemap;

        public void DestroyCubemap()
        {
            if (cubemap != null)
            {
                UnityEngine.Object.Destroy(cubemap);
                cubemap = null;
            }
        }

        public Cubemap GetCubemap()
        {
            if (cubemap != null)
            {
                return cubemap;
            }

            Texture2D texture = null;
            try
            {
                var file = CombinedTexture;
                if (string.IsNullOrEmpty(file) && !string.IsNullOrEmpty(FilePrefix) && !IsSplitFormat)
                {
                    file = FilePrefix + "cubemap.png";
                }

                if (string.IsNullOrEmpty(file))
                {
                    throw new InvalidOperationException("Combined texture file name is empty");
                }

                var path = Path.Combine(Directory, file);
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

                ExtractFaces(texture, size);

                cubemap.anisoLevel = 9;
                cubemap.filterMode = FilterMode.Trilinear;
                cubemap.SmoothEdges();
                cubemap.Apply();

                return cubemap;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load cubemap {Code}: {e.Message}");
                Debug.LogException(e);

                if (cubemap != null)
                {
                    UnityEngine.Object.Destroy(cubemap);
                    cubemap = null;
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

        private void ExtractFaces(Texture2D texture, int size)
        {
            ExtractFace(texture, CubemapFace.PositiveY, size, size * 2, size);
            ExtractFace(texture, CubemapFace.NegativeX, 0, size, size);
            ExtractFace(texture, CubemapFace.PositiveZ, size, size, size);
            ExtractFace(texture, CubemapFace.PositiveX, size * 2, size, size);
            ExtractFace(texture, CubemapFace.NegativeZ, size * 3, size, size);
            ExtractFace(texture, CubemapFace.NegativeY, size, 0, size);
        }

        private void ExtractFace(Texture2D texture, CubemapFace face, int startX, int startY, int size)
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

                    flipped[dst] = pixels[src];
                }
            }

            cubemap.SetPixels(flipped, face);
        }
    }
}
