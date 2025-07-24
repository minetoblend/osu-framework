// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Framework.Graphics
{
    public partial class DoomPath : SmoothPath
    {
        public bool DoomMode { get; init; }

        private IntPtr doomFb;

        private Image<Rgba32> image = new Image<Rgba32>(800, 1280);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!DoomMode)
                return;

            doomFb = doomgeneric_dll_init();

            new Thread(() =>
            {
                const int size = 1280 * 800 * 4;
                byte[] managedArray = new byte[size];

                while (!IsDisposed)
                {
                    doomgeneric_dll_tick([]);

                    Marshal.Copy(doomFb, managedArray, 0, size);

                    var img = Image.LoadPixelData<Rgba32>(managedArray, 1280, 800);
                    img.Mutate(it => it.Rotate(RotateMode.Rotate90));

                    image = img;
                    InvalidateTexture();
                }
            }).Start();
        }

        protected override Image<Rgba32> GetImageData(int textureWidth)
        {
            return DoomMode ? image : base.GetImageData(textureWidth);
        }

        // protected override Image<Rgba32> GetImageData(int textureWidth) => image;

        [DllImport("doomgeneric.dll", EntryPoint = "doomgeneric_dll_init", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern IntPtr doomgeneric_dll_init();

        [DllImport("doomgeneric.dll", EntryPoint = "doomgeneric_dll_tick", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern void doomgeneric_dll_tick(byte[] kbd);
    }
}
