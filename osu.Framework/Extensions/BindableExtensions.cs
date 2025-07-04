// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using osu.Framework.Bindables;

namespace osu.Framework.Extensions
{
    public static class BindableExtensions
    {
        /// <summary>
        /// Creates a readonly <see cref="IBindable{T}"/> with it's value automatically assigned from the source <see cref="IBindable{T}"/> and converted using the given transform function.
        /// </summary>
        public static IBindable<TDest> Map<TSource, TDest>(this IBindable<TSource> source, Func<TSource, TDest> transform)
        {
            var dest = new Bindable<TDest>();

            dest.ComputeFrom(source, transform);

            return dest;
        }

        /// <summary>
        /// Binds a <see cref="Bindable{T}"/> to another <see cref="IBindable{T}"/> with the value automatically converted using the given transform function.
        /// </summary>
        public static void ComputeFrom<TSource, TDest>(this Bindable<TDest> dest, IBindable<TSource> source, Func<TSource, TDest> transform)
        {
            source.BindValueChanged(e =>
            {
                dest.Value = transform(e.NewValue);
            }, true);

            source.BindDisabledChanged(disabled =>
            {
                dest.Disabled = disabled;
            }, true);
        }

        /// <summary>
        /// Bidirectionally syncs the value of two <see cref="Bindable{T}"/>s with the two given transform functions.
        /// </summary>
        public static Bindable<TDest> SyncWith<TSource, TDest>(this Bindable<TDest> dest, Bindable<TSource> source, Func<TSource, TDest> toDest, Func<TDest, TSource> toSource)
        {
            dest.ComputeFrom(source, toDest);

            dest.BindValueChanged(e =>
            {
                source.Value = toSource(e.NewValue);
            });

            dest.BindDisabledChanged(disabled =>
            {
                source.Disabled = disabled;
            });

            return dest;
        }

        public delegate bool SafeMappingFunction<in TSource, TDest>(TSource value, [MaybeNullWhen(false)] out TDest result);

        /// <summary>
        /// Bidirectionally syncs the value of two <see cref="Bindable{T}"/>s with the two given transform functions, with the ability to
        /// reset the state based on the source <see cref="Bindable{T}"/> if the destination <see cref="Bindable{T}"/>'s value becomes invalid.
        /// </summary>
        public static void SyncWith<TSource, TDest>(this Bindable<TDest> dest, Bindable<TSource> source, Func<TSource, TDest> toDest, SafeMappingFunction<TDest, TSource> tryParse)
        {
            source.BindValueChanged(e =>
            {
                dest.Value = toDest(e.NewValue);
            }, true);

            dest.BindValueChanged(e =>
            {
                if (tryParse(e.NewValue, out var result))
                    source.Value = result;
                else
                    source.TriggerChange();
            });
        }

        /// <summary>
        /// Bidirectionally syncs the value of a <see cref="Bindable{T}"/> with a string value with a <see cref="Bindable{T}"/> with an <see cref="int"/> value.
        /// </summary>
        public static void SyncWith(this Bindable<string> dest, Bindable<int> source, [StringSyntax("NumericFormat")] string? format = null, IFormatProvider? formatProvider = null) =>
            dest.SyncWith(source, value => value.ToString(format, formatProvider), (string str, out int result) => int.TryParse(str, formatProvider, out result));

        /// <summary>
        /// Bidirectionally syncs the value of a <see cref="Bindable{T}"/> with a string value with a <see cref="Bindable{T}"/> with an <see cref="float"/> value.
        /// </summary>
        public static void SyncWith(this Bindable<string> dest, Bindable<float> source, [StringSyntax("NumericFormat")] string? format = null, IFormatProvider? formatProvider = null) =>
            dest.SyncWith(source, value => value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture), (string str, out float result) => float.TryParse(str, formatProvider, out result));

        /// <summary>
        /// Bidirectionally syncs the value of a <see cref="Bindable{T}"/> with a string value with a <see cref="Bindable{T}"/> with an <see cref="float"/> value.
        /// </summary>
        public static void SyncWith(this Bindable<string> dest, Bindable<double> source, [StringSyntax("NumericFormat")] string? format = null, IFormatProvider? formatProvider = null) =>
            dest.SyncWith(source, value => value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture), (string str, out double result) => double.TryParse(str, formatProvider, out result));
    }
}
