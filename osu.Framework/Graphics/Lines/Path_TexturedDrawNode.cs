// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp.Processing;

namespace osu.Framework.Graphics.Lines
{
    public partial class Path
    {
        public partial class TexturedPathDrawNode : DrawNode
        {
            private const int max_res = 24;

            protected new Path Source => (Path)base.Source;

            private readonly List<Line> segments = new List<Line>();

            private Texture? texture;
            private Vector2 drawSize;
            private float radius;
            private float distanceScale;
            private IShader? pathShader;

            private IVertexBatch<TexturedVertex3D>? triangleBatch;

            public TexturedPathDrawNode(IDrawable source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                segments.Clear();
                segments.AddRange(Source.segments);

                texture = Source.Texture;
                drawSize = Source.DrawSize;
                radius = Source.PathRadius;
                pathShader = Source.pathShader;
                if (texture != null)
                    distanceScale = texture.Width / radius / 4;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (texture?.Available != true || segments.Count == 0 || pathShader == null)
                    return;

                // We multiply the size args by 3 such that the amount of vertices is a multiple of the amount of vertices
                // per primitive (triangles in this case). Otherwise overflowing the batch will result in wrong
                // grouping of vertices into primitives.
                triangleBatch ??= renderer.CreateLinearBatch<TexturedVertex3D>(max_res * 200 * 3, 10, PrimitiveTopology.Triangles);

                renderer.PushLocalMatrix(DrawInfo.Matrix);
                renderer.PushDepthInfo(DepthInfo.Default);

                // Blending is removed to allow for correct blending between the wedges of the path.
                renderer.SetBlend(BlendingParameters.None);

                pathShader.Bind();

                texture.Bind();

                updateVertexBuffer();

                pathShader.Unbind();

                renderer.PopDepthInfo();
                renderer.PopLocalMatrix();
            }

            private Vector2 relativePosition(Vector2 localPos) => Vector2.Divide(localPos, drawSize);

            private Vector2 pointOnCircle(float angle) => new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            private Color4 colourAt(Vector2 localPos) => DrawColourInfo.Colour.TryExtractSingleColour(out SRGBColour colour)
                ? colour.SRGB
                : DrawColourInfo.Colour.Interpolate(relativePosition(localPos)).SRGB;

            private void updateVertexBuffer()
            {
                Debug.Assert(texture != null);
                Debug.Assert(segments.Count > 0);

                RectangleF texRect = texture.GetTextureRect();
                RectangleF singlePixel = texture.GetTextureRect(new RectangleF(0, 0f, texture.DisplayWidth, 1));

                RectangleF coordsRect = singlePixel with { Height = 0 };

                Line? prevSegmentLeft = null;
                Line? prevSegmentRight = null;

                float totalDistance = 0;

                for (int i = 0; i < segments.Count; i++)
                {
                    Line currSegment = segments[i];

                    coordsRect.Height = currSegment.Rho * distanceScale * singlePixel.Height;

                    Vector2 ortho = currSegment.OrthogonalDirection;
                    if (float.IsNaN(ortho.X) || float.IsNaN(ortho.Y))
                        ortho = Vector2.UnitY;

                    Line currSegmentLeft = new Line(currSegment.StartPoint + ortho * radius, currSegment.EndPoint + ortho * radius);
                    Line currSegmentRight = new Line(currSegment.StartPoint - ortho * radius, currSegment.EndPoint - ortho * radius);

                    if (i == 0)
                    {
                        float height = singlePixel.Height * radius * distanceScale;

                        addEndCap(currSegment.StartPoint, currSegmentLeft.StartPoint, texRect, coordsRect with { Height = height });

                        coordsRect.Y += height;

                        // Line flippedLeft = new Line(currSegmentRight.EndPoint, currSegmentRight.StartPoint);
                        // Line flippedRight = new Line(currSegmentLeft.EndPoint, currSegmentLeft.StartPoint);
                        //
                        // coordsRect.Y += addSegmentCaps(MathF.PI, currSegmentLeft, currSegmentRight, flippedLeft, flippedRight, texRect, coordsRect with { Height = singlePixel.Height });
                    }

                    if (prevSegmentLeft is Line psLeft && prevSegmentRight is Line psRight)
                    {
                        Debug.Assert(i > 0);

                        // Connection/filler caps between segment quads
                        float thetaDiff = currSegment.Theta - segments[i - 1].Theta;
                        coordsRect.Y += addSegmentCaps(thetaDiff, currSegmentLeft, currSegmentRight, psLeft, psRight, texRect, coordsRect with { Height = singlePixel.Height });
                    }

                    addSegmentQuads(currSegment, currSegmentLeft, currSegmentRight, texRect, coordsRect);

                    coordsRect.Y += coordsRect.Height;

                    if (i == segments.Count - 1)
                    {
                        float height = singlePixel.Height * radius * distanceScale;

                        var rect = coordsRect with { Height = height };

                        // flipping the rect to undo the flip from the rotation

                        rect.X += rect.Width;
                        rect.Width *= -1;

                        rect.Y += rect.Height;
                        rect.Height *= -1;

                        addEndCap(currSegment.EndPoint, currSegmentRight.EndPoint, texRect, rect);

                        // Line flippedLeft = new Line(currSegmentRight.EndPoint, currSegmentRight.StartPoint);
                        // Line flippedRight = new Line(currSegmentLeft.EndPoint, currSegmentLeft.StartPoint);
                        //
                        // addSegmentCaps(MathF.PI, flippedLeft, flippedRight, currSegmentLeft, currSegmentRight, texRect, coordsRect with { Height = singlePixel.Height });
                    }

                    prevSegmentLeft = currSegmentLeft;
                    prevSegmentRight = currSegmentRight;
                }
            }

            private void addSegmentQuads(Line segment, Line segmentLeft, Line segmentRight, RectangleF texRect, RectangleF coordsRect)
            {
                Debug.Assert(triangleBatch != null);

                // Each segment of the path is actually rendered as 2 quads, being split in half along the approximating line.
                // On this line the depth is 1 instead of 0, which is done in order to properly handle self-overlap using the depth buffer.
                Vector3 firstMiddlePoint = new Vector3(segment.StartPoint.X, segment.StartPoint.Y, 1);
                Vector3 secondMiddlePoint = new Vector3(segment.EndPoint.X, segment.EndPoint.Y, 1);
                Color4 firstMiddleColour = colourAt(segment.StartPoint);
                Color4 secondMiddleColour = colourAt(segment.EndPoint);

                // Each of the quads (mentioned above) is rendered as 2 triangles:
                // Outer quad, triangle 1
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = new Vector3(segmentRight.EndPoint.X, segmentRight.EndPoint.Y, 0),
                    TexturePosition = coordsRect.BottomRight,
                    Colour = colourAt(segmentRight.EndPoint),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = new Vector3(segmentRight.StartPoint.X, segmentRight.StartPoint.Y, 0),
                    TexturePosition = coordsRect.TopRight,
                    Colour = colourAt(segmentRight.StartPoint),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = firstMiddlePoint,
                    TexturePosition = coordsRect.TopCentre,
                    Colour = firstMiddleColour,
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });

                // Outer quad, triangle 2
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = firstMiddlePoint,
                    TexturePosition = coordsRect.TopCentre,
                    Colour = firstMiddleColour,
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = secondMiddlePoint,
                    TexturePosition = coordsRect.BottomCentre,
                    Colour = secondMiddleColour,
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = new Vector3(segmentRight.EndPoint.X, segmentRight.EndPoint.Y, 0),
                    TexturePosition = coordsRect.BottomRight,
                    Colour = colourAt(segmentRight.EndPoint),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });

                // Inner quad, triangle 1
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = firstMiddlePoint,
                    TexturePosition = coordsRect.TopCentre,
                    Colour = firstMiddleColour,
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = secondMiddlePoint,
                    TexturePosition = coordsRect.BottomCentre,
                    Colour = secondMiddleColour,
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = new Vector3(segmentLeft.EndPoint.X, segmentLeft.EndPoint.Y, 0),
                    TexturePosition = coordsRect.BottomLeft,
                    Colour = colourAt(segmentLeft.EndPoint),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });

                // Inner quad, triangle 2
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = new Vector3(segmentLeft.EndPoint.X, segmentLeft.EndPoint.Y, 0),
                    TexturePosition = coordsRect.BottomLeft,
                    Colour = colourAt(segmentLeft.EndPoint),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = new Vector3(segmentLeft.StartPoint.X, segmentLeft.StartPoint.Y, 0),
                    TexturePosition = coordsRect.TopLeft,
                    Colour = colourAt(segmentLeft.StartPoint),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
                triangleBatch.Add(new TexturedVertex3D
                {
                    Position = firstMiddlePoint,
                    TexturePosition = coordsRect.TopCentre,
                    Colour = firstMiddleColour,
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                });
            }

            private float addSegmentCaps(float thetaDiff, Line segmentLeft, Line segmentRight, Line prevSegmentLeft, Line prevSegmentRight, RectangleF texRect, RectangleF singlePixel)
            {
                Debug.Assert(triangleBatch != null);

                if (Math.Abs(thetaDiff) > MathF.PI)
                    thetaDiff = -Math.Sign(thetaDiff) * 2 * MathF.PI + thetaDiff;

                if (thetaDiff == 0f)
                    return 0;

                Vector2 origin = (segmentLeft.StartPoint + segmentRight.StartPoint) / 2;

                // Use segment end points instead of calculating start/end via theta to guarantee
                // that the vertices have the exact same position as the quads, which prevents
                // possible pixel gaps during rasterization.
                Vector2 current = thetaDiff > 0f ? prevSegmentRight.EndPoint : prevSegmentLeft.EndPoint;
                Vector2 end = thetaDiff > 0f ? segmentRight.StartPoint : segmentLeft.StartPoint;

                Line start = thetaDiff > 0f ? new Line(prevSegmentLeft.EndPoint, prevSegmentRight.EndPoint) : new Line(prevSegmentRight.EndPoint, prevSegmentLeft.EndPoint);
                float theta0 = start.Theta;
                float thetaStep = Math.Sign(thetaDiff) * MathF.PI / max_res;
                int stepCount = (int)MathF.Ceiling(thetaDiff / thetaStep);

                Color4 originColour = colourAt(origin);
                Color4 currentColour = colourAt(current);

                float distanceTravelled = 0;

                var coordsRect = singlePixel with { Height = 0 };

                var originTexCoord = coordsRect.BottomCentre;

                for (int i = 1; i <= stepCount; i++)
                {
                    var next = i < stepCount ? origin + pointOnCircle(theta0 + i * thetaStep) * radius : end;

                    float distance = Vector2.Distance(current, next) * distanceScale * singlePixel.Height / 1;

                    coordsRect.Y += coordsRect.Height;
                    coordsRect.Height = distance;

                    distanceTravelled += distance;

                    // Center point
                    triangleBatch.Add(new TexturedVertex3D
                    {
                        Position = new Vector3(origin.X, origin.Y, 1),
                        TexturePosition = originTexCoord,
                        Colour = originColour,
                        TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    });

                    // First outer point
                    triangleBatch.Add(new TexturedVertex3D
                    {
                        Position = new Vector3(current.X, current.Y, 0),
                        TexturePosition = thetaDiff < 0 ? coordsRect.TopLeft : coordsRect.TopRight,
                        Colour = currentColour,
                        TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    });

                    current = next;
                    currentColour = colourAt(current);

                    // Second outer point
                    triangleBatch.Add(new TexturedVertex3D
                    {
                        Position = new Vector3(current.X, current.Y, 0),
                        TexturePosition = thetaDiff < 0 ? coordsRect.BottomLeft : coordsRect.BottomRight,
                        Colour = currentColour,
                        TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    });
                }

                return distanceTravelled;
            }

            private void addEndCap(Vector2 origin, Vector2 start, RectangleF texRect, RectangleF coordsRect)
            {
                Debug.Assert(triangleBatch != null);

                const float thetaDiff = MathF.PI;

                // Use segment end points instead of calculating start/end via theta to guarantee
                // that the vertices have the exact same position as the quads, which prevents
                // possible pixel gaps during rasterization.
                Vector2 current = start;
                Vector2 end = origin * 2 - start;

                float theta0 = new Line(origin, start).Theta;
                float thetaStep = Math.Sign(thetaDiff) * MathF.PI / max_res;
                int stepCount = (int)MathF.Ceiling(thetaDiff / thetaStep);

                Color4 originColour = colourAt(origin);
                Color4 currentColour = colourAt(current);

                var originTexCoord = coordsRect.BottomCentre;

                for (int i = 1; i <= stepCount; i++)
                {
                    var next = i < stepCount ? origin + pointOnCircle(theta0 + i * thetaStep) * radius : end;

                    // Center point
                    triangleBatch.Add(new TexturedVertex3D
                    {
                        Position = new Vector3(origin.X, origin.Y, 1),
                        TexturePosition = originTexCoord,
                        Colour = originColour,
                        TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    });

                    // First outer point
                    triangleBatch.Add(new TexturedVertex3D
                    {
                        Position = new Vector3(current.X, current.Y, 0),
                        TexturePosition = texCoordFor(i - 1),
                        Colour = currentColour,
                        TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    });

                    current = next;
                    currentColour = colourAt(current);

                    // Second outer point
                    triangleBatch.Add(new TexturedVertex3D
                    {
                        Position = new Vector3(current.X, current.Y, 0),
                        TexturePosition = texCoordFor(i),
                        Colour = currentColour,
                        TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    });
                }

                Vector2 texCoordFor(int index)
                {
                    var relative = pointOnCircle(index * thetaStep);

                    return new Vector2(
                        float.Lerp(coordsRect.Right, coordsRect.Left, relative.X * 0.5f + 0.5f),
                        float.Lerp(coordsRect.Bottom, coordsRect.Top, relative.Y)
                    );
                }
            }
        }
    }
}
