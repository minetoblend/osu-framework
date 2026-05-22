// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Logging;
using osuTK.Input;

namespace osu.Framework.Input.Handlers.Keyboard
{
    public class KeyboardLatencyProbe
    {
        // ---- Win32 ----
        private const int wm_input = 0x00FF;
        private const int wm_close = 0x0010;
        private const uint rid_input = 0x10000003;
        private const uint rim_typekeyboard = 1;
        private const uint ridev_inputsink = 0x00000100;
        private const uint ridev_remove = 0x00000001;
        private static readonly IntPtr hwnd_message = new IntPtr(-3);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rawinputdevice
        {
            public ushort UsagePage, Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rawinputheader
        {
            public uint Type, Size;
            public IntPtr Device, WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rawkeyboard
        {
            public ushort MakeCode, Flags, Reserved, VKey;
            public uint Message, ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rawinput
        {
            public Rawinputheader Header;
            public Rawkeyboard Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Wndclass
        {
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra, cbWndExtra;
            public IntPtr hInstance, hIcon, hCursor, hbrBackground;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Msg
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam, lParam;
            public uint time;
            public int pt_x, pt_y;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassW(ref Wndclass c);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(uint ex, string cls, string name, uint style,
                                                     int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProcW(IntPtr h, uint m, IntPtr w, IntPtr l);

        [DllImport("user32.dll")]
        private static extern int GetMessageW(out Msg m, IntPtr h, uint a, uint b);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessageW(ref Msg m);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr h);

        [DllImport("user32.dll")]
        private static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices([In] Rawinputdevice[] d, uint n, uint s);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr h, uint c, IntPtr d, ref uint s, uint hs);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandleW(string m);

        // ---- Matching state ----

        private readonly struct PendingRawEvent
        {
            public readonly long TimestampTicks;
            public readonly bool IsDown;

            public PendingRawEvent(long ts, bool down)
            {
                TimestampTicks = ts;
                IsDown = down;
            }
        }

        // Key: (scancode << 1) | (extended ? 1 : 0). Value: FIFO of pending Raw Input timestamps.
        private readonly Dictionary<int, Queue<PendingRawEvent>> pending = new Dictionary<int, Queue<PendingRawEvent>>();
        private readonly object @lock = new object();

        // Ring buffer of recent samples for reading out stats.
        private readonly double[] samplesMs;
        private int sampleHead;
        private int sampleCount;
        private long totalMatched;
        private long totalUnmatched;

        // ---- Thread state ----
        private readonly Thread thread;
        private readonly ManualResetEventSlim ready = new ManualResetEventSlim(false);
        private IntPtr hwnd;
        private WndProcDelegate wndProcKeepAlive = null!;
        private volatile bool running;

        /// <summary>Fired on the Raw Input thread for every key event.</summary>
        public event Action<double>? LatencyMeasured;

        public KeyboardLatencyProbe(int sampleBufferSize = 1024)
        {
            samplesMs = new double[sampleBufferSize];
            thread = new Thread(threadProc)
            {
                IsBackground = true,
                Name = "RawInputLatencyProbe",
                Priority = ThreadPriority.Highest,
            };
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(10));

            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    Logger.Log(FormatStats());
                }
            }).Start();
        }

        /// <summary>
        /// Call from your SDL event handler. Pass the <c>raw</c> field of <c>SDL_KeyboardEvent</c>
        /// (the platform scancode) and whether it's a key-down.
        /// Returns the measured latency in milliseconds, or null if no match was found.
        /// </summary>
        public double? RecordSdlEvent(ushort sdlRawScancode, bool isDown)
        {
            long nowTicks = Stopwatch.GetTimestamp();
            int key = makeKey(sdlRawScancode, isExtended: false);
            int keyExt = makeKey((ushort)(sdlRawScancode & 0x7F), isExtended: true);

            lock (@lock)
            {
                // SDL's "raw" on Windows is the scancode with the extended bit folded in
                // various ways depending on key — try both keyings.
                if (tryDequeue(key, isDown, out var pendingEvent) ||
                    tryDequeue(keyExt, isDown, out pendingEvent))
                {
                    double ms = (nowTicks - pendingEvent.TimestampTicks) * 1000.0 / Stopwatch.Frequency;
                    samplesMs[sampleHead] = ms;
                    sampleHead = (sampleHead + 1) % samplesMs.Length;
                    if (sampleCount < samplesMs.Length) sampleCount++;
                    totalMatched++;
                    LatencyMeasured?.Invoke(ms);
                    return ms;
                }

                totalUnmatched++;
                return null;
            }
        }

        /// <summary>Snapshot of measured latencies (in ms). Safe to call from any thread.</summary>
        public LatencyStats GetStats()
        {
            lock (@lock)
            {
                if (sampleCount == 0)
                    return new LatencyStats(0, 0, 0, 0, 0, totalMatched, totalUnmatched);

                double min = double.MaxValue, max = 0, sum = 0;
                double[] copy = new double[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    double v = samplesMs[i];
                    copy[i] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                }

                Array.Sort(copy);
                double p50 = copy[copy.Length / 2];
                double p99 = copy[Math.Min(copy.Length - 1, (int)(copy.Length * 0.99))];
                return new LatencyStats(min, sum / sampleCount, p50, p99, max, totalMatched, totalUnmatched);
            }
        }

        public readonly record struct LatencyStats(
            double MinMs,
            double MeanMs,
            double P50Ms,
            double P99Ms,
            double MaxMs,
            long Matched,
            long Unmatched);

        // ---- Internals ----

        private static int makeKey(ushort scancode, bool isExtended)
            => (scancode << 1) | (isExtended ? 1 : 0);

        private bool tryDequeue(int key, bool isDown, out PendingRawEvent value)
        {
            if (pending.TryGetValue(key, out var q))
            {
                // Pop entries until we find one matching down/up, discarding stale mismatches.
                while (q.Count > 0)
                {
                    var head = q.Peek();
                    // Drop entries older than 250ms — likely stale (e.g. focus loss).
                    double ageMs = (Stopwatch.GetTimestamp() - head.TimestampTicks) * 1000.0 / Stopwatch.Frequency;

                    if (ageMs > 250)
                    {
                        q.Dequeue();
                        continue;
                    }

                    if (head.IsDown != isDown) break;

                    value = q.Dequeue();
                    return true;
                }
            }

            value = default;
            return false;
        }

        private void threadProc()
        {
            wndProcKeepAlive = wndProc;
            nint hInstance = GetModuleHandleW(null!);
            const string class_name = "KeyboardLatencyProbeSink";

            var wc = new Wndclass
            {
                lpfnWndProc = wndProcKeepAlive,
                hInstance = hInstance,
                lpszClassName = class_name,
            };

            if (RegisterClassW(ref wc) == 0)
                throw new InvalidOperationException("RegisterClass failed: " + Marshal.GetLastWin32Error());

            hwnd = CreateWindowExW(0, class_name, "", 0, 0, 0, 0, 0,
                hwnd_message, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());

            var rid = new Rawinputdevice
            {
                UsagePage = 0x01, Usage = 0x06,
                Flags = ridev_inputsink, Target = hwnd,
            };
            if (!RegisterRawInputDevices(new[] { rid }, 1, (uint)Marshal.SizeOf<Rawinputdevice>()))
                throw new InvalidOperationException("RegisterRawInputDevices failed: " + Marshal.GetLastWin32Error());

            running = true;
            ready.Set();

            while (running && GetMessageW(out Msg msg, IntPtr.Zero, 0, 0) > 0)
                DispatchMessageW(ref msg);
        }

        private IntPtr wndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == wm_input)
            {
                long ts = Stopwatch.GetTimestamp();
                uint size = 0;
                uint headerSize = (uint)Marshal.SizeOf<Rawinputheader>();
                GetRawInputData(lParam, rid_input, IntPtr.Zero, ref size, headerSize);

                IntPtr buf = Marshal.AllocHGlobal((int)size);

                try
                {
                    if (GetRawInputData(lParam, rid_input, buf, ref size, headerSize) == size)
                    {
                        var raw = Marshal.PtrToStructure<Rawinput>(buf);

                        if (raw.Header.Type == rim_typekeyboard)
                        {
                            var kb = raw.Keyboard;

                            // Ignore the fake shift keys Windows synthesises for numpad/extended keys
                            if (kb.VKey == 0xFF) return IntPtr.Zero;

                            bool isDown = (kb.Flags & 0x01) == 0;
                            bool isExtended = (kb.Flags & 0x02) != 0;

                            // Resolve generic VK_SHIFT/VK_CONTROL/VK_MENU to L/R variants
                            ushort vk = normalizeVk(kb.VKey, kb.MakeCode, isExtended);

                            lock (@lock)
                            {
                                if (!pending.TryGetValue(vk, out var q))
                                    pending[vk] = q = new Queue<PendingRawEvent>(8);

                                Logger.Log($"enqueuing key event vk=0x{vk:X2},isDown={isDown}");

                                q.Enqueue(new PendingRawEvent(ts, isDown));
                            }
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }

                return IntPtr.Zero;
            }

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private static ushort normalizeVk(ushort vk, ushort scanCode, bool isExtended)
        {
            const ushort vk_shift = 0x10, vk_control = 0x11, vk_menu = 0x12;
            const ushort vk_lshift = 0xA0, vk_rshift = 0xA1;
            const ushort vk_lcontrol = 0xA2, vk_rcontrol = 0xA3;
            const ushort vk_lmenu = 0xA4, vk_rmenu = 0xA5;

            switch (vk)
            {
                case vk_shift:
                    return scanCode == 0x36 ? vk_rshift : vk_lshift;

                case vk_control:
                    return isExtended ? vk_rcontrol : vk_lcontrol;

                case vk_menu:
                    return isExtended ? vk_rmenu : vk_lmenu;

                default:
                    return vk;
            }
        }

        public void Dispose()
        {
            if (hwnd != IntPtr.Zero)
            {
                var rid = new Rawinputdevice
                {
                    UsagePage = 0x01, Usage = 0x06,
                    Flags = ridev_remove, Target = IntPtr.Zero,
                };
                RegisterRawInputDevices(new[] { rid }, 1, (uint)Marshal.SizeOf<Rawinputdevice>());

                running = false;
                PostMessageW(hwnd, wm_close, IntPtr.Zero, IntPtr.Zero);
                DestroyWindow(hwnd);
                hwnd = IntPtr.Zero;
            }

            thread.Join(1000);
        }

        public string FormatStats()
        {
            var s = GetStats();
            if (s.Matched == 0)
                return $"[KeyboardLatency] no samples yet (unmatched: {s.Unmatched})";

            return $"[KeyboardLatency] n={s.Matched} " +
                   $"min={s.MinMs:F2}ms " +
                   $"mean={s.MeanMs:F2}ms " +
                   $"p50={s.P50Ms:F2}ms " +
                   $"p99={s.P99Ms:F2}ms " +
                   $"max={s.MaxMs:F2}ms " +
                   $"(unmatched: {s.Unmatched})";
        }

        public double? RecordKeyEvent(Key key, bool isDown)
        {
            ushort? vk = osuTkKeyToVk(key);

            if (vk is null)
            {
                Logger.Log($"Could not convert key {key} to VK");
                return null;
            }

            return RecordKeyEventByVk(vk.Value, isDown);
        }

        public double? RecordKeyEventByVk(ushort vk, bool isDown)
        {
            long nowTicks = Stopwatch.GetTimestamp();

            lock (@lock)
            {
                if (tryDequeue(vk, isDown, out var pendingEvent))
                {
                    Logger.Log($"dequeued key event vk={vk},isDown={isDown}");

                    double ms = (nowTicks - pendingEvent.TimestampTicks) * 1000.0 / Stopwatch.Frequency;
                    samplesMs[sampleHead] = ms;
                    sampleHead = (sampleHead + 1) % samplesMs.Length;
                    if (sampleCount < samplesMs.Length) sampleCount++;
                    totalMatched++;
                    LatencyMeasured?.Invoke(ms);
                    return ms;
                }
                else
                {
                    Logger.Log($"failed to dequeue key event vk={vk},isDown={isDown}");
                }

                totalUnmatched++;
                return null;
            }
        }

        private static ushort? osuTkKeyToVk(Key key)
        {
            switch (key)
            {
                // Letters
                case Key.A: return 0x41;

                case Key.B: return 0x42;

                case Key.C: return 0x43;

                case Key.D: return 0x44;

                case Key.E: return 0x45;

                case Key.F: return 0x46;

                case Key.G: return 0x47;

                case Key.H: return 0x48;

                case Key.I: return 0x49;

                case Key.J: return 0x4A;

                case Key.K: return 0x4B;

                case Key.L: return 0x4C;

                case Key.M: return 0x4D;

                case Key.N: return 0x4E;

                case Key.O: return 0x4F;

                case Key.P: return 0x50;

                case Key.Q: return 0x51;

                case Key.R: return 0x52;

                case Key.S: return 0x53;

                case Key.T: return 0x54;

                case Key.U: return 0x55;

                case Key.V: return 0x56;

                case Key.W: return 0x57;

                case Key.X: return 0x58;

                case Key.Y: return 0x59;

                case Key.Z: return 0x5A;

                // Number row
                case Key.Number0: return 0x30;

                case Key.Number1: return 0x31;

                case Key.Number2: return 0x32;

                case Key.Number3: return 0x33;

                case Key.Number4: return 0x34;

                case Key.Number5: return 0x35;

                case Key.Number6: return 0x36;

                case Key.Number7: return 0x37;

                case Key.Number8: return 0x38;

                case Key.Number9: return 0x39;

                // Modifiers — distinguished L/R
                case Key.LShift: return 0xA0;

                case Key.RShift: return 0xA1;

                case Key.LControl: return 0xA2;

                case Key.RControl: return 0xA3;

                case Key.LAlt: return 0xA4;

                case Key.RAlt: return 0xA5;

                case Key.LWin: return 0x5B;

                case Key.RWin: return 0x5C;

                // Common others
                case Key.Space: return 0x20;

                case Key.Enter: return 0x0D;

                case Key.Escape: return 0x1B;

                case Key.Tab: return 0x09;

                case Key.BackSpace: return 0x08;

                case Key.Left: return 0x25;

                case Key.Up: return 0x26;

                case Key.Right: return 0x27;

                case Key.Down: return 0x28;

                case Key.Insert: return 0x2D;

                case Key.Delete: return 0x2E;

                case Key.Home: return 0x24;

                case Key.End: return 0x23;

                case Key.PageUp: return 0x21;

                case Key.PageDown: return 0x22;

                // Function keys
                case Key.F1: return 0x70;

                case Key.F2: return 0x71;

                case Key.F3: return 0x72;

                case Key.F4: return 0x73;

                case Key.F5: return 0x74;

                case Key.F6: return 0x75;

                case Key.F7: return 0x76;

                case Key.F8: return 0x77;

                case Key.F9: return 0x78;

                case Key.F10: return 0x79;

                case Key.F11: return 0x7A;

                case Key.F12: return 0x7B;

                default: return null; // extend as needed
            }
        }
    }
}
