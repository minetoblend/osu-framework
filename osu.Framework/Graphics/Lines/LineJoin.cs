// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Graphics.Lines
{
    /// <summary>
    /// Style for joining together path segments
    /// </summary>
    public enum LineJoin
    {
        /// <summary>
        /// The corners of the path are rounded
        /// </summary>
        Round,

        /// <summary>
        /// The corners of the path are squared off
        /// </summary>
        Bevel,

        /// <summary>
        /// The corners of the path are extended to meet at a point
        /// </summary>
        Miter,
    }
}
