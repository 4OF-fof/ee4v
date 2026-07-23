using System;
using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public sealed class SettingRange<T>
    {
        private readonly Func<SettingValidationResult> _outOfRangeResult;

        public SettingRange(
            T minimum,
            T maximum,
            Func<SettingValidationResult> outOfRangeResult)
        {
            if (Comparer<T>.Default.Compare(minimum, maximum) > 0)
            {
                throw new ArgumentException("The minimum setting value must not exceed the maximum value.");
            }

            Minimum = minimum;
            Maximum = maximum;
            _outOfRangeResult = outOfRangeResult ?? throw new ArgumentNullException(nameof(outOfRangeResult));
        }

        public T Minimum { get; }

        public T Maximum { get; }

        public bool Contains(T value)
        {
            return Comparer<T>.Default.Compare(value, Minimum) >= 0 &&
                   Comparer<T>.Default.Compare(value, Maximum) <= 0;
        }

        public T Clip(T value)
        {
            if (Comparer<T>.Default.Compare(value, Minimum) < 0)
            {
                return Minimum;
            }

            return Comparer<T>.Default.Compare(value, Maximum) > 0
                ? Maximum
                : value;
        }

        internal SettingValidationResult CreateOutOfRangeResult()
        {
            return _outOfRangeResult();
        }
    }
}
