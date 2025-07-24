// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics
{
    public partial class Doom : CompositeDrawable
    {
        [DllImport("doomgeneric.dll", EntryPoint = "doomgeneric_dll_init", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern IntPtr doomgeneric_dll_init();

        [DllImport("doomgeneric.dll", EntryPoint = "doomgeneric_dll_tick", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern void doomgeneric_dll_tick(byte[] kbd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        private Texture texture = null!;

        private IntPtr doomFb;

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            texture = renderer.CreateTexture(1280, 800);

            InternalChild = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Texture = texture,

                FillMode = FillMode.Fit
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            doomFb = doomgeneric_dll_init();
        }

        protected override void Update()
        {
            base.Update();

            doomgeneric_dll_tick([]);

            const int size = 1280 * 800 * 4;
            byte[] managedArray = new byte[size];
            Marshal.Copy(doomFb, managedArray, 0, size);

            var image = Image.LoadPixelData<Rgba32>(managedArray, 1280, 800);

            texture.SetData(new TextureUpload(image));
        }
    }
}
