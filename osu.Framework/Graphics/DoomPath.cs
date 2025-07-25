// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Lines;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics
{
    public partial class DoomPath : SmoothPath
    {
        public bool DoomMode { get; init; }

        private DoomStatic.Doom? doom;

        [Resolved]
        private GameHost host { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!DoomMode)
                return;

            DoomStatic.Host ??= host;
            doom = DoomStatic.Instance.Value;

            doom.OnNewFrame += InvalidateTexture;
        }

        protected override Image<Rgba32> GetImageData(int textureWidth) => doom?.FrameBuffer?.Clone() ?? base.GetImageData(textureWidth);

        protected override void Dispose(bool isDisposing)
        {
            if (doom != null)
                doom.OnNewFrame -= InvalidateTexture;

            base.Dispose(isDisposing);
        }
    }
}
