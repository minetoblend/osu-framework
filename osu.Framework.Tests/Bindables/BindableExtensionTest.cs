// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
            Assert.IsTrue(mapped2.Disabled);
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

            source.Value = 5;

            Assert.AreEqual(5, source.Value);
            Assert.AreEqual("5", dest.Value);
            Assert.AreEqual(1, sourceChanged);
            Assert.AreEqual(2, destChanged);

            dest.Value = "-10";

            Assert.AreEqual(-10, source.Value);
            Assert.AreEqual("-10", dest.Value);
            Assert.AreEqual(2, sourceChanged);
            Assert.AreEqual(3, destChanged);
        }
    }
}
