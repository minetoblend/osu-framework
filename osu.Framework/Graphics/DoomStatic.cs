// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Framework.Graphics
{
    public class DoomStatic
    {
        public static Lazy<Doom> Instance = new Lazy<Doom>(() => new Doom());

        public static GameHost Host = null!;

        public class Doom
        {
            public event Action? OnNewFrame;

            private readonly IntPtr doomFb;
            private readonly byte[] doomKbd = new byte[256];
            public Image<Rgba32> FrameBuffer { get; private set; } = new Image<Rgba32>(800, 1280);

            public Doom()
            {
                Host.Window.Update += () =>
                {
                    GetKeyboardState(doomKbd);
                };

                doomFb = doomgeneric_dll_init();

                new Thread(() =>
                {
                    const int size = 1280 * 800 * 4;
                    byte[] managedArray = new byte[size];

                    while (true)
                    {
                        doomgeneric_dll_tick(doomKbd);

                        Marshal.Copy(doomFb, managedArray, 0, size);

                        var img = Image.LoadPixelData<Rgba32>(managedArray, 1280, 800);
                        img.Mutate(it => it.Rotate(RotateMode.Rotate90));

                        FrameBuffer = img;

                        OnNewFrame?.Invoke();
                    }
                }).Start();
            }
        }

        [DllImport("doomgeneric.dll", EntryPoint = "doomgeneric_dll_init", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern IntPtr doomgeneric_dll_init();

        [DllImport("doomgeneric.dll", EntryPoint = "doomgeneric_dll_tick", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        private static extern void doomgeneric_dll_tick(byte[] kbd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);
    }
}
