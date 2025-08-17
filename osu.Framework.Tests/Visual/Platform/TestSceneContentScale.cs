// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;

namespace osu.Framework.Tests.Visual.Platform
{
    public partial class TestSceneContentScale : FrameworkTestScene
    {
        private readonly Container absoluteScaleContainer;
        private readonly Container scaleContainer;
        private readonly SpriteText scaleText;

        [Resolved]
        private GameHost host { get; set; } = null!;

        public TestSceneContentScale()
        {
            Add(absoluteScaleContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = scaleContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(10),
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(10),
                            Children = new Drawable[]
                            {
                                scaleText = new SpriteText
                                {
                                    Font = new FontUsage(size: 24)
                                },
                                new SizeBox
                                {
                                    Size = new Vector2(100),
                                },
                                new SizeBox
                                {
                                    Size = new Vector2(200, 30),
                                }
                            }
                        }
                    }
                }
            });
        }

        protected override void Update()
        {
            base.Update();

            float scale = DrawInfo.MatrixInverse.ExtractScale().X;

            absoluteScaleContainer.Scale = new Vector2(scale);
            absoluteScaleContainer.Size = new Vector2(1 / scale);

            scaleContainer.Scale = new Vector2(host.Window.ContentScale);
            scaleContainer.Size = new Vector2(1 / host.Window.ContentScale);

            scaleText.Text = $"Scale: {host.Window.ContentScale:P0}";
        }

        private partial class SizeBox : Container
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                AddRangeInternal(new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new SpriteText
                    {
                        Text = $"{Width}px",
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Colour = Color4.Black,
                        Font = new FontUsage(size: 14),
                    },
                    new SpriteText
                    {
                        Text = $"{Height}px",
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.TopCentre,
                        Rotation = -90,
                        Colour = Color4.Black,
                        Font = new FontUsage(size: 14),
                    }
                });
            }
        }
    }
}
