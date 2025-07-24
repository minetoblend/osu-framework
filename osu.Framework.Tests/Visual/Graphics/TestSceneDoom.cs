// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Testing;

namespace osu.Framework.Tests.Visual.Graphics
{
    public partial class TestSceneDoom : TestScene
    {
        public TestSceneDoom()
        {
            Child = new Doom
            {
                RelativeSizeAxes = Axes.Both,
            };
        }
    }
}
