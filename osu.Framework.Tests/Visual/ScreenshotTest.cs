// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Visualisation;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;

namespace osu.Framework.Tests.Visual
{
    [TestFixture]
    public partial class ScreenshotTest : TestScene
    {
        [Test]
        public void TestScreenshot()
        {
            Drawable drawable = null!;

            Logger.Log("foo");

            AddStep("add child", () => Child = drawable = new Container
            {
                Size = new Vector2(200),
                Child = new Box
                {
                    Size = new Vector2(100),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.Red,
                }
            });

            bool screenshotTaken = false;

            AddStep("take screenshot", () =>
            {
                Logger.Log("Taking screenshot...");

                screenshotTaken = false;
                Add(new DrawableScreenshotter(drawable, image =>
                {
                    Logger.Log($"image is null: {image == null}");
                    screenshotTaken = true;
                }));
            });

            AddWaitStep("wait", 10);

            AddAssert("screenshot taken", () => screenshotTaken);
        }
    }
}
