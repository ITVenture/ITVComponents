using System;
using System.Collections.Generic;

namespace ITVComponents.MemberAccess
{
    /// <summary>
    /// A dotted path (a.b.c) that reads and writes the same target.
    /// </summary>
    /// <remarks>
    /// The n-1 leading segments are walked weakly: a null on the way yields nothing when reading.
    /// Writing is strict - a null on the way is an error, because nothing here knows what kind of
    /// object would have to be created there.
    /// </remarks>
    public sealed class MemberPath
    {
        /// <summary>
        /// the character that separates the segments of a path
        /// </summary>
        public const char Separator = '.';

        /// <summary>
        /// the segments of this path
        /// </summary>
        private readonly string[] segments;

        /// <summary>
        /// Initializes a new instance of the MemberPath class
        /// </summary>
        /// <param name="path">the path this instance stands for</param>
        /// <param name="segments">the segments of the given path</param>
        private MemberPath(string path, string[] segments)
        {
            Path = path;
            this.segments = segments;
        }

        /// <summary>
        /// Gets the path this instance stands for
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the segments of this path
        /// </summary>
        public IReadOnlyList<string> Segments => segments;

        /// <summary>
        /// Gets a value indicating whether this path addresses a member of the root object itself
        /// </summary>
        public bool IsSingleSegment => segments.Length == 1;

        /// <summary>
        /// Parses the given path
        /// </summary>
        /// <param name="path">the path to parse</param>
        /// <returns>the parsed path</returns>
        public static MemberPath Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            var parts = path.Split(Separator);
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    throw new MemberAccessFailedException($"'{path}' is not a valid member-path: it has an empty segment.");
                }
            }

            return new MemberPath(path, parts);
        }

        /// <summary>
        /// Reads the value this path addresses
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        /// <returns>the value the path addresses, or null when something on the way is null</returns>
        public object Read(object root, IMemberAccessGuard guard = null)
        {
            if (!TryGetSlot(root, guard, out var slot))
            {
                return null;
            }

            return slot.Read();
        }

        /// <summary>
        /// Writes the value this path addresses
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="value">the value to write</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        public void Write(object root, object value, IMemberAccessGuard guard = null)
        {
            GetSlot(root, guard).Write(value);
        }

        /// <summary>
        /// Gets the slot this path addresses, and fails when something on the way is null
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        /// <returns>the slot this path addresses</returns>
        public MemberSlot GetSlot(object root, IMemberAccessGuard guard = null)
        {
            var target = Walk(root, guard, out var failedAt);
            if (target == null)
            {
                throw new MemberAccessFailedException(
                    $"Unable to resolve '{Path}': '{failedAt}' is null.", MemberAccessResult.NotFound);
            }

            return MemberAccessor.Resolve(target, segments[segments.Length - 1], null, guard);
        }

        /// <summary>
        /// Tries to get the slot this path addresses
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        /// <param name="slot">the slot this path addresses</param>
        /// <returns>a value indicating whether the path could be walked to its last segment</returns>
        public bool TryGetSlot(object root, IMemberAccessGuard guard, out MemberSlot slot)
        {
            slot = null;
            var target = Walk(root, guard, out _);
            if (target == null)
            {
                return false;
            }

            slot = MemberAccessor.Resolve(target, segments[segments.Length - 1], null, guard);
            return true;
        }

        /// <summary>
        /// Reads the value the given path addresses on the given object
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="path">the path to read</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        /// <returns>the value the path addresses</returns>
        public static object Read(object root, string path, IMemberAccessGuard guard = null)
        {
            return Parse(path).Read(root, guard);
        }

        /// <summary>
        /// Writes the value the given path addresses on the given object
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="path">the path to write</param>
        /// <param name="value">the value to write</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        public static void Write(object root, string path, object value, IMemberAccessGuard guard = null)
        {
            Parse(path).Write(root, value, guard);
        }

        /// <summary>
        /// Walks the leading segments of this path
        /// </summary>
        /// <param name="root">the object the path starts at</param>
        /// <param name="guard">the guard that decides whether the access may happen</param>
        /// <param name="failedAt">the part of the path that turned out to be null</param>
        /// <returns>the object the last segment is to be resolved on, or null</returns>
        private object Walk(object root, IMemberAccessGuard guard, out string failedAt)
        {
            failedAt = null;
            var current = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (current == null)
                {
                    failedAt = Describe(i);
                    return null;
                }

                var slot = MemberAccessor.Resolve(current, segments[i], null, guard);
                var result = slot.TryRead(out current);
                if (result == MemberAccessResult.NotReadable)
                {
                    current = null;
                }
                else if (result != MemberAccessResult.Ok)
                {
                    throw new MemberAccessFailedException(
                        $"Unable to resolve '{Path}': {slot.Describe(result, false)}", result);
                }
            }

            if (current == null)
            {
                failedAt = Describe(segments.Length - 1);
            }

            return current;
        }

        /// <summary>
        /// Names the part of the path that has been walked so far
        /// </summary>
        /// <param name="count">the number of segments that have been walked</param>
        /// <returns>the walked part of the path, or a name for the root object</returns>
        private string Describe(int count)
        {
            return count == 0 ? "the root object" : string.Join(Separator.ToString(), segments, 0, count);
        }
    }
}
