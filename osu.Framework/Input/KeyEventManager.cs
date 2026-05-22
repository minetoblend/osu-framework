// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.Handlers.Keyboard;
using osu.Framework.Input.States;
using osu.Framework.Logging;
using osuTK.Input;

namespace osu.Framework.Input
{
    /// <summary>
    /// Manages state events for a single key.
    /// </summary>
    public class KeyEventManager : ButtonEventManager<Key>
    {
        private static readonly KeyboardLatencyProbe latency_probe = new KeyboardLatencyProbe();

        public KeyEventManager(Key key)
            : base(key)
        {
        }

        public void HandleRepeat(InputState state)
        {
            // Only drawables that can still handle input should handle the repeat
            var drawables = ButtonDownInputQueue.AsNonNull().Intersect(InputQueue);

            PropagateButtonEvent(drawables, new KeyDownEvent(state, Button, true));
        }

        protected override Drawable? HandleButtonDown(InputState state, List<Drawable> targets)
        {
            double? duration = latency_probe.RecordKeyEvent(Button, true);
            Logger.Log($"duration for keydown({Button})={duration}");
            return PropagateButtonEvent(targets, new KeyDownEvent(state, Button));
        }

        protected override void HandleButtonUp(InputState state, List<Drawable> targets)
        {
            double? duration = latency_probe.RecordKeyEvent(Button, false);
            Logger.Log($"duration for keyup({Button})={duration}");
            PropagateButtonEvent(targets, new KeyUpEvent(state, Button));
        }

        protected override bool SuppressLoggingEventInformation(Drawable drawable) => drawable is ICanSuppressKeyEventLogging canSuppress && canSuppress.SuppressKeyEventLogging;
    }
}
