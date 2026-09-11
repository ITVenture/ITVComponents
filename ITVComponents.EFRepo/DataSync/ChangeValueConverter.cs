using System;
using System.Globalization;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Turns the raw text of a <see cref="Models.ChangeDetail"/> into the value that the target property expects.
    /// <para>
    /// Configuration payloads travel between systems, therefore every value is read exactly the way it was written:
    /// culture-invariant, timestamps in UTC. The generic <see cref="System.Convert.ChangeType(object,Type)"/> - which
    /// used to be the default behind the assignment-expression - did neither: it parsed under the culture of the user
    /// who applied the change (silently turning "12.50" into 1250 on a de-DE or de UI) and it throws on enum- and
    /// nullable-targets, which takes the whole change down with it and leaves the record unwritten.
    /// </para>
    /// </summary>
    public static class ChangeValueConverter
    {
        /// <summary>
        /// Converts the raw text of a change-detail into the target-type of the property it is assigned to
        /// </summary>
        /// <param name="rawValue">the value as it was written into the configuration-payload</param>
        /// <param name="targetType">the type of the property that receives the value</param>
        /// <returns>the converted value</returns>
        public static object ToTypedValue(string rawValue, Type targetType)
        {
            if (targetType == null)
            {
                // Nothing is known about the target - the assignment itself will complain if the text does not fit.
                return rawValue;
            }

            var effective = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effective == typeof(string))
            {
                return rawValue;
            }

            if (string.IsNullOrEmpty(rawValue))
            {
                if (!targetType.IsValueType || effective != targetType)
                {
                    // a reference- or nullable-target: empty text means "no value"
                    return null;
                }

                throw new FormatException(
                    $"An empty value can not be assigned to the non-nullable type '{targetType.FullName}'.");
            }

            if (effective.IsEnum)
            {
                // Covers both spellings a payload may use: the name of the value and its number.
                return Enum.Parse(effective, rawValue, true);
            }

            if (effective == typeof(Guid))
            {
                return Guid.Parse(rawValue);
            }

            if (effective == typeof(TimeSpan))
            {
                return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);
            }

            if (effective == typeof(DateTime))
            {
                // Always UTC: a stamp without zone-information is read as UTC, one with an offset is converted into
                // it. Kind=Local is never produced - PostgreSQL refuses that on a "timestamp with time zone".
                return DateTime.Parse(rawValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            }

            if (effective == typeof(DateTimeOffset))
            {
                return DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            }

            if (effective == typeof(DateOnly))
            {
                return DateOnly.Parse(rawValue, CultureInfo.InvariantCulture);
            }

            if (effective == typeof(TimeOnly))
            {
                return TimeOnly.Parse(rawValue, CultureInfo.InvariantCulture);
            }

            if (effective == typeof(byte[]))
            {
                return System.Convert.FromBase64String(rawValue);
            }

            if (effective == typeof(Uri))
            {
                return new Uri(rawValue, UriKind.RelativeOrAbsolute);
            }

            if (effective == typeof(Version))
            {
                return Version.Parse(rawValue);
            }

            return System.Convert.ChangeType(rawValue, effective, CultureInfo.InvariantCulture);
        }
    }
}
