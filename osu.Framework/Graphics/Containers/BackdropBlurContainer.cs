// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Layout;
using osuTK;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Containers
{
    /// <summary>
    /// A container that blurs the content of its nearest parent <see cref="IBackbufferProvider"/> behind its children.
    /// If all children are of a specific non-<see cref="Drawable"/> type, use the
    /// generic version <see cref="BackdropBlurContainer{T}"/>.
    /// </summary>
    public partial class BackdropBlurContainer : BackdropBlurContainer<Drawable>
    {
    }

    /// <summary>
    /// A container that blurs the content of its nearest parent <see cref="IBufferedContainer"/> behind its children.
    /// </summary>
    public partial class BackdropBlurContainer<T> : Container<T>, IBufferedContainer, IBackdropBlurDrawable where T : Drawable
    {
        public Vector2 BlurSigma { get; set; } = Vector2.Zero;

        public float BlurRotation { get; set; }

        public virtual float BackdropOpacity => 1 - MathF.Pow(1 - base.DrawColourInfo.Colour.MaxAlpha, 2);

        public float MaskCutoff { get; set; }

        public float BackdropTintStrength { get; set; }

        public Vector2 EffectBufferScale { get; set; } = Vector2.One;

        [Resolved]
        private IBackbufferProvider backbufferProvider { get; set; } = null!;

        public IShader TextureShader { get; private set; } = null!;

        private readonly BufferedDrawNodeSharedData sharedData;

        public BackdropBlurContainer(RenderBufferFormat[]? formats = null)
        {
            sharedData = new BufferedDrawNodeSharedData(1, formats, clipToRootNode: true);
        }

        IShader IBackdropBlurDrawable.BlurShader => blurShader;

        IShader IBackdropBlurDrawable.BackdropBlurShader => backdropBlurShader;

        private IShader blurShader = null!;

        private IShader backdropBlurShader = null!;

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            blurShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.BLUR);
            backdropBlurShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.BACKDROP_BLUR);
        }

        protected override DrawNode CreateDrawNode() => new BackdropBlurContainerDrawNode(this, new CompositeDrawableDrawNode(this), sharedData);

        private RectangleF lastBackBufferDrawRect;

        RectangleF IBackdropBlurDrawable.LastBackBufferDrawRect => lastBackBufferDrawRect;

        protected override void Update()
        {
            base.Update();

            Invalidate(Invalidation.DrawNode);

            lastBackBufferDrawRect = backbufferProvider.ScreenSpaceDrawQuad.AABBFloat;
        }

        private bool hadParent;

        protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
        {
            if ((invalidation & Invalidation.Parent) > 0 && backbufferProvider is RefCountedBackbufferProvider refCount)
            {
                bool hasParent = Parent != null;

                if (hasParent != hadParent)
                {
                    if (hasParent)
                        refCount.Increment();
                    else
                        refCount.Decrement();

                    hadParent = hasParent;
                }
            }

            return base.OnInvalidate(invalidation, source);
        }

        public Color4 BackgroundColour => Color4.Transparent;
        public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;

        // Children should not receive the true colour to avoid colour doubling when the frame-buffers are rendered to the back-buffer.
        public override DrawColourInfo DrawColourInfo
        {
            get
            {
                // Todo: This is incorrect.
                var blending = Blending;
                blending.ApplyDefaultToInherited();

                return new DrawColourInfo(Color4.White, blending);
            }
        }

        public Vector2 FrameBufferScale { get; set; } = Vector2.One;

        protected override void Dispose(bool isDisposing)
        {
            if (hadParent && backbufferProvider is RefCountedBackbufferProvider refCount)
            {
                refCount.Decrement();
                hadParent = false;
            }

            base.Dispose(isDisposing);
        }

        private class BackdropBlurContainerDrawNode : BackdropBlurDrawNode, ICompositeDrawNode
        {
            public BackdropBlurContainerDrawNode(IBufferedDrawable source, CompositeDrawableDrawNode child, BufferedDrawNodeSharedData sharedData)
                : base(source, child, sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();
            }

            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            public List<DrawNode> Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;
        }
    }
}