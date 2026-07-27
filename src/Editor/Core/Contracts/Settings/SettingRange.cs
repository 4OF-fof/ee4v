using System;
using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public sealed class SettingRange<T>
    {
        private readonly IComparer<T> _comparer;
        private readonly Func<SettingValidationResult> _outOfRangeResult;

        public SettingRange(
            T minimum,
            T maximum,
            Func<SettingValidationResult> outOfRangeResult = null,
            IComparer<T> comparer = null)
        {
            _comparer = comparer ?? Comparer<T>.Default;
            if (_comparer.Compare(minimum, maximum) > 0)
            {
                throw new ArgumentException("Minimum must not exceed maximum.");
            }

            Minimum = minimum;
            Maximum = maximum;
            _outOfRangeResult = outOfRangeResult;
        }

        public T Minimum { get; }

        public T Maximum { get; }

        public bool Contains(T value)
        {
            return _comparer.Compare(value, Minimum) >= 0 &&
                   _comparer.Compare(value, Maximum) <= 0;
        }

        public SettingValidationResult CreateOutOfRangeResult()
        {
            return _outOfRangeResult != null
                ? _outOfRangeResult()
                : SettingValidationResult.Error("The setting value is outside the allowed range.");
        }
    }
}
