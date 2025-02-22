// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Drawing.Tests
{
    /// <summary>
    /// A test image file.
    /// </summary>
    public class TestFormat : IConfigurationModule, IImageFormat
    {
        private readonly Dictionary<Type, object> sampleImages = new Dictionary<Type, object>();

        // We should not change Configuration.Default in individual tests!
        // Create new configuration instances with new Configuration(TestFormat.GlobalTestFormat) instead!
        public static TestFormat GlobalTestFormat { get; } = new TestFormat();

        public TestFormat()
        {
            this.Encoder = new TestEncoder(this);
            this.Decoder = new TestDecoder(this);
        }

        public List<DecodeOperation> DecodeCalls { get; } = new List<DecodeOperation>();

        public IImageEncoder Encoder { get; }

        public IImageDecoder Decoder { get; }

        private readonly byte[] header = Guid.NewGuid().ToByteArray();

        public MemoryStream CreateStream(byte[] marker = null)
        {
            var ms = new MemoryStream();
            byte[] data = this.header;
            ms.Write(data, 0, data.Length);
            if (marker != null)
            {
                ms.Write(marker, 0, marker.Length);
            }

            ms.Position = 0;
            return ms;
        }

        public void VerifySpecificDecodeCall<TPixel>(byte[] marker, Configuration config)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            DecodeOperation[] discovered = this.DecodeCalls.Where(x => x.IsMatch(marker, config, typeof(TPixel))).ToArray();

            Assert.True(discovered.Any(), "No calls to decode on this format with the provided options happened");

            foreach (DecodeOperation d in discovered)
            {
                this.DecodeCalls.Remove(d);
            }
        }

        public void VerifyAgnosticDecodeCall(byte[] marker, Configuration config)
        {
            DecodeOperation[] discovered = this.DecodeCalls.Where(x => x.IsMatch(marker, config, typeof(TestPixelForAgnosticDecode))).ToArray();

            Assert.True(discovered.Any(), "No calls to decode on this format with the provided options happened");

            foreach (DecodeOperation d in discovered)
            {
                this.DecodeCalls.Remove(d);
            }
        }

        public Image<TPixel> Sample<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel>
        {
            lock (this.sampleImages)
            {
                if (!this.sampleImages.ContainsKey(typeof(TPixel)))
                {
                    this.sampleImages.Add(typeof(TPixel), new Image<TPixel>(1, 1));
                }

                return (Image<TPixel>)this.sampleImages[typeof(TPixel)];
            }
        }

        public Image SampleAgnostic() => this.Sample<TestPixelForAgnosticDecode>();

        public string MimeType => "img/test";

        public string Extension => "test_ext";

        public IEnumerable<string> SupportedExtensions => new[] { "test_ext" };

        public int HeaderSize => this.header.Length;

        public string Name => this.Extension;

        public string DefaultMimeType => this.MimeType;

        public IEnumerable<string> MimeTypes => new[] { this.MimeType };

        public IEnumerable<string> FileExtensions => this.SupportedExtensions;

        public bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            if (header.Length < this.header.Length)
            {
                return false;
            }

            for (int i = 0; i < this.header.Length; i++)
            {
                if (header[i] != this.header[i])
                {
                    return false;
                }
            }

            return true;
        }

        public void Configure(Configuration host)
        {
            host.ImageFormatsManager.AddImageFormatDetector(new TestHeader(this));
            host.ImageFormatsManager.SetEncoder(this, new TestEncoder(this));
            host.ImageFormatsManager.SetDecoder(this, new TestDecoder(this));
        }

        public struct DecodeOperation
        {
            public byte[] Marker { get; set; }

            internal Configuration Config { get; set; }

            public Type PixelType { get; set; }

            public bool IsMatch(byte[] testMarker, Configuration config, Type pixelType)
            {
                if (this.Config != config || this.PixelType != pixelType)
                {
                    return false;
                }

                if (testMarker.Length != this.Marker.Length)
                {
                    return false;
                }

                for (int i = 0; i < this.Marker.Length; i++)
                {
                    if (testMarker[i] != this.Marker[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public class TestHeader : IImageFormatDetector
        {
            private readonly TestFormat testFormat;

            public TestHeader(TestFormat testFormat) => this.testFormat = testFormat;

            public int HeaderSize => this.testFormat.HeaderSize;

            public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
            {
                if (this.testFormat.IsSupportedFileFormat(header))
                {
                    return this.testFormat;
                }

                return null;
            }
        }

        public class TestDecoder : IImageDecoder
        {
            private readonly TestFormat testFormat;

            public TestDecoder(TestFormat testFormat) => this.testFormat = testFormat;

            public IEnumerable<string> MimeTypes => new[] { this.testFormat.MimeType };

            public IEnumerable<string> FileExtensions => this.testFormat.SupportedExtensions;

            public int HeaderSize => this.testFormat.HeaderSize;

            public Image<TPixel> Decode<TPixel>(Configuration config, Stream stream)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] marker = ms.ToArray().Skip(this.testFormat.header.Length).ToArray();
                this.testFormat.DecodeCalls.Add(new DecodeOperation
                {
                    Marker = marker,
                    Config = config,
                    PixelType = typeof(TPixel)
                });

                // TODO record this happened so we can verify it.
                return this.testFormat.Sample<TPixel>();
            }

            public bool IsSupportedFileFormat(Span<byte> header) => this.testFormat.IsSupportedFileFormat(header);

            public Image Decode(Configuration configuration, Stream stream) => this.Decode<TestPixelForAgnosticDecode>(configuration, stream);

            public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
                where TPixel : unmanaged, IPixel<TPixel>
                => throw new NotImplementedException();

            public Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
                => throw new NotImplementedException();
        }

        public class TestEncoder : IImageEncoder
        {
            private readonly TestFormat testFormat;

            public TestEncoder(TestFormat testFormat)
                => this.testFormat = testFormat;

            public IEnumerable<string> MimeTypes => new[] { this.testFormat.MimeType };

            public IEnumerable<string> FileExtensions => this.testFormat.SupportedExtensions;

            public void Encode<TPixel>(Image<TPixel> image, Stream stream)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                // TODO record this happened so we can verify it.
            }

            public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
                where TPixel : unmanaged, IPixel<TPixel>
                => throw new NotImplementedException();
        }

        private struct TestPixelForAgnosticDecode : IPixel<TestPixelForAgnosticDecode>
        {
            public PixelOperations<TestPixelForAgnosticDecode> CreatePixelOperations() => new();

            public void FromScaledVector4(Vector4 vector)
            {
            }

            public Vector4 ToScaledVector4() => default;

            public void FromVector4(Vector4 vector)
            {
            }

            public Vector4 ToVector4() => default;

            public void FromArgb32(Argb32 source)
            {
            }

            public void FromBgra5551(Bgra5551 source)
            {
            }

            public void FromBgr24(Bgr24 source)
            {
            }

            public void FromBgra32(Bgra32 source)
            {
            }

            public void FromL8(L8 source)
            {
            }

            public void FromL16(L16 source)
            {
            }

            public void FromLa16(La16 source)
            {
            }

            public void FromLa32(La32 source)
            {
            }

            public void FromRgb24(Rgb24 source)
            {
            }

            public void FromRgba32(Rgba32 source)
            {
            }

            public void ToRgba32(ref Rgba32 dest)
            {
            }

            public void FromRgb48(Rgb48 source)
            {
            }

            public void FromRgba64(Rgba64 source)
            {
            }

            public void FromAbgr32(Abgr32 source)
            {
            }

            public bool Equals(TestPixelForAgnosticDecode other) => false;
        }
    }
}
