// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Graphics.Lines
{
    /// <summary>
    /// Style for the ends of a path
    /// </summary>
    public enum LineCap
    {
        /// <summary>
        /// The ends of the path are rounded
        /// </summary>
        Round,

        /// <summary>
        /// The ends of the path are squared off by adding a box with an equal width and half the height of the line's thickness.
        /// </summary>
        Square,
    }
}
