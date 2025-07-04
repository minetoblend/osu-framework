// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions;

namespace osu.Framework.Tests.Bindables
{
    [TestFixture]
    public class BindableExtensionTest
    {
        [Test]
        public void TestMappedBindable()
        {
            var source = new Bindable<int>();

            var mapped1 = source.Map(v => v.ToString());
            var mapped2 = source.Map(v => v * 2);

            int changed1 = 0;
            int changed2 = 0;

            mapped1.ValueChanged += _ => changed1++;
            mapped2.ValueChanged += _ => changed2++;

            Assert.AreEqual(mapped1.Value, "0");

            source.Value = 3;

            Assert.AreEqual(mapped1.Value, "3");
            Assert.AreEqual(changed1, 1);

            Assert.AreEqual(mapped2.Value, 6);
            Assert.AreEqual(changed2, 1);

            source.Value = -10;

            Assert.AreEqual(mapped1.Value, "-10");
            Assert.AreEqual(changed1, 2);

            Assert.AreEqual(mapped2.Value, -20);
            Assert.AreEqual(changed2, 2);

            source.Disabled = true;
            Assert.IsTrue(mapped1.Disabled);
            Assert.IsTrue(mapped2.Disabled);

            source.Disabled = false;
            Assert.IsFalse(mapped1.Disabled);
            Assert.IsFalse(mapped2.Disabled);
        }

        [Test]
        public void TestSyncedBindable()
        {
            var source = new Bindable<int>();
            var dest = new Bindable<string>();

            int sourceChanged = 0;
            int destChanged = 0;

            source.ValueChanged += _ => sourceChanged++;
            dest.ValueChanged += _ => destChanged++;

            dest.SyncWith(source, value => value.ToString(), int.Parse);

            Assert.AreEqual(0, source.Value);
            Assert.AreEqual("0", dest.Value);
            Assert.AreEqual(0, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            source.Value = 5;

            Assert.AreEqual(5, source.Value);
            Assert.AreEqual("5", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "-10";

            Assert.AreEqual(-10, source.Value);
            Assert.AreEqual("-10", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            void resetCount()
            {
                sourceChanged = 0;
                destChanged = 0;
            }
        }

        [Test]
        public void TestSafeSyncedBindable()
        {
            var source = new Bindable<int>();
            var dest = new Bindable<string>();

            int sourceChanged = 0;
            int destChanged = 0;

            source.ValueChanged += _ => sourceChanged++;
            dest.ValueChanged += _ => destChanged++;

            dest.SyncWith(source, value => value.ToString(), int.TryParse);

            Assert.AreEqual(0, source.Value);
            Assert.AreEqual("0", dest.Value);
            Assert.AreEqual(0, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            source.Value = 5;

            Assert.AreEqual(5, source.Value);
            Assert.AreEqual("5", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "-10";

            Assert.AreEqual(-10, source.Value);
            Assert.AreEqual("-10", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "invalid value";

            Assert.AreEqual(-10, source.Value);
            Assert.AreEqual("-10", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            void resetCount()
            {
                sourceChanged = 0;
                destChanged = 0;
            }
        }

        [Test]
        public void TestSyncWithInt()
        {
            var source = new BindableInt();
            var dest = new Bindable<string>();

            int sourceChanged = 0;
            int destChanged = 0;

            source.ValueChanged += _ => sourceChanged++;
            dest.ValueChanged += _ => destChanged++;

            dest.SyncWith(source);

            Assert.AreEqual(0, source.Value);
            Assert.AreEqual("0", dest.Value);
            Assert.AreEqual(0, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            source.Value = 5;

            Assert.AreEqual(5, source.Value);
            Assert.AreEqual("5", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "-10";

            Assert.AreEqual(-10, source.Value);
            Assert.AreEqual("-10", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "invalid value";

            Assert.AreEqual(-10, source.Value);
            Assert.AreEqual("-10", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            resetCount();

            source.MaxValue = 10;
            dest.Value = "20";

            Assert.AreEqual(10, source.Value);
            Assert.AreEqual("10", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            void resetCount()
            {
                sourceChanged = 0;
                destChanged = 0;
            }
        }

        [Test]
        public void TestSyncWithFloat()
        {
            var source = new BindableFloat();
            var dest = new Bindable<string>();

            int sourceChanged = 0;
            int destChanged = 0;

            source.ValueChanged += _ => sourceChanged++;
            dest.ValueChanged += _ => destChanged++;

            dest.SyncWith(source);

            Assert.AreEqual(0, source.Value);
            Assert.AreEqual("0", dest.Value);
            Assert.AreEqual(0, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            source.Value = 5.3f;

            Assert.AreEqual(5.3f, source.Value);
            Assert.AreEqual("5.3", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "-10.9";

            Assert.AreEqual(-10.9f, source.Value);
            Assert.AreEqual("-10.9", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "invalid value";
            Assert.AreEqual(-10.9f, source.Value);
            Assert.AreEqual("-10.9", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            resetCount();

            source.MaxValue = 10.2f;
            dest.Value = "20";

            Assert.AreEqual(10.2f, source.Value);
            Assert.AreEqual("10.2", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            resetCount();

            source.Precision = 0.01f;
            dest.Value = Math.PI.ToString(CultureInfo.InvariantCulture);

            Assert.AreEqual(3.14f, source.Value);
            Assert.AreEqual("3.14", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            void resetCount()
            {
                sourceChanged = 0;
                destChanged = 0;
            }
        }

        [Test]
        public void TestSyncWithDouble()
        {
            var source = new BindableDouble();
            var dest = new Bindable<string>();

            int sourceChanged = 0;
            int destChanged = 0;

            source.ValueChanged += _ => sourceChanged++;
            dest.ValueChanged += _ => destChanged++;

            dest.SyncWith(source);

            Assert.AreEqual(0, source.Value);
            Assert.AreEqual("0", dest.Value);
            Assert.AreEqual(0, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            source.Value = 5.3;

            Assert.AreEqual(5.3, source.Value);
            Assert.AreEqual("5.3", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "-10.9";

            Assert.AreEqual(-10.9, source.Value);
            Assert.AreEqual("-10.9", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(1, destChanged);

            resetCount();

            dest.Value = "invalid value";
            Assert.AreEqual(-10.9, source.Value);
            Assert.AreEqual("-10.9", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            resetCount();

            source.MaxValue = 10.2;
            dest.Value = "20";

            Assert.AreEqual(10.2, source.Value);
            Assert.AreEqual("10.2", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            resetCount();

            source.Precision = 0.01;
            dest.Value = Math.PI.ToString(CultureInfo.InvariantCulture);

            Assert.AreEqual(3.14, source.Value);
            Assert.AreEqual("3.14", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            void resetCount()
            {
                sourceChanged = 0;
                destChanged = 0;
            }
        }
    }
}
