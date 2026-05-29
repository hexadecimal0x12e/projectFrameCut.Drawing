using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
#pragma warning disable IDE1006 // this is a legacy problem from the early design of the project, and changing it will cause a lot of codebase compatibility problems. So we have to disable this warning for the properties in this interface.

namespace projectFrameCut.Drawing.Base
{
    /// <summary>
    /// Represents a picture with pixel data of type T and a float alpha channel.
    /// </summary>
    /// <typeparam name="T">The pixel type.</typeparam>
    public interface IPicture<T> : IPicture
    {
        /// <summary>
        /// Red channel. 
        /// </summary>
        /// <remarks>
        /// the positive minimum of <typeparamref name="T"/> means dark and the positive maximum of <typeparamref name="T"/> means brightest.
        /// </remarks>
        [JsonIgnore()]
        public T[] r { get; set; }
        /// <summary>
        /// Green channel.
        /// </summary>
        /// <remarks>
        /// the positive minimum of <typeparamref name="T"/> means dark and the positive maximum of <typeparamref name="T"/> means brightest.
        /// </remarks>
        [JsonIgnore()]
        public T[] g { get; set; }
        /// <summary>
        /// Blue channel.
        /// </summary>
        /// <remarks>
        /// the positive minimum of <typeparamref name="T"/> means dark and the positive maximum of <typeparamref name="T"/> means brightest.
        /// </remarks>
        [JsonIgnore()]
        public T[] b { get; set; }
        /// <summary>
        /// Alpha channel. 0 means completely transparent and 1 means not transparent.
        /// Negative value is not accepted.
        /// If this array is null, means this image does not have alpha channel.
        /// </summary>
        [JsonIgnore()]
        [NotNull()]
        public float[]? a { get; set; }

        /// <summary>
        /// Set the alpha channel.
        /// </summary>
        public IPicture<T> SetAlpha(bool haveAlpha);
    }

    /// <summary>
    /// The structure of a monochrome picture, which only has one gray channel and an optional alpha channel.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMonoChromePicture<T> : IPicture
    {
        /// <summary>
        /// The gray channel.
        /// </summary>
        /// <remarks>
        /// the positive minimum of <typeparamref name="T"/> means dark and the positive maximum of <typeparamref name="T"/> means brightest.
        /// </remarks>
        [JsonIgnore()]
        public T[] gray { get; set; }

        /// <summary>
        /// Alpha channel. 0 means completely transparent and 1 means not transparent.
        /// Negative value is not accepted.
        /// If this array is null, means this image does not have alpha channel.
        /// </summary>
        [JsonIgnore()]
        [NotNull()]
        public float[]? a { get; set; }

        public new bool HasAlphaChannel { get => false; set { } }
    }

    /// <summary>
    /// The structure of a HDR Picture.
    /// </summary>
    public interface IHDRPicture<T> : IPicture<T>
    {
        /// <summary>
        /// Get the brightness for each pixel.
        /// </summary>
        /// <remarks>
        /// for each item, 1 means as same bright as <see cref="MaximumBrightness"/>; 
        /// 0 means no brightness (same as dark)
        /// Negative value is not accepted.
        /// </remarks>
        [JsonIgnore()]
        public float[] Brightness { get; set; }

        /// <summary>
        /// Get or set the maximum brightness (unit in nit) of this picture.
        /// </summary>
        public float MaximumBrightness { get; set; }
    }


    /// <summary>
    /// Represents a picture without an alpha channel.
    /// </summary>
    public interface INoAlphaPicture<T> : IPicture
    {
        [JsonIgnore()]
        public T[] r { get; set; }
        [JsonIgnore()]
        public T[] g { get; set; }
        [JsonIgnore()]
        public T[] b { get; set; }

        public new bool HasAlphaChannel { get => false; set { } }
    }
    /// <summary>
    /// Represents a picture with an uniform, float-based alpha channel.
    /// </summary>
    public interface IUniformAlphaPicture<T> : IPicture
    {
        [JsonIgnore()]
        public T[] r { get; set; }
        [JsonIgnore()]
        public T[] g { get; set; }
        [JsonIgnore()]
        public T[] b { get; set; }
        [JsonIgnore()]
        public float uniformAlpha { get; set; }
    }

}
