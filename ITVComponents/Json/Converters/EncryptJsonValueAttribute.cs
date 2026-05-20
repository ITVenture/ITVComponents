using System;

namespace ITVComponents.Json.Converters
{
    /// <summary>
    /// Strategy-neutral marker for a sensitive string member. Unlike
    /// <c>[JsonConverter(typeof(JsonStringEncryptConverter))]</c> (which hard-binds the System.Text.Json
    /// converter), this attribute carries no framework type. Each <see cref="Strategy.IJsonStrategy"/>
    /// implementation inspects it and wires up its own encrypt converter, so the same model encrypts
    /// correctly whether serialized with the native (System.Text.Json) or the Newtonsoft strategy.
    /// </summary>
    /// <remarks>
    /// Mirrors the encryption rules of <see cref="JsonStringEncryptConverter"/>: a value prefixed with
    /// <c>encrypt:</c> is encrypted on write; everything else is written verbatim. With no
    /// <see cref="Entropy"/> the default AES key (see <c>PasswordSecurity.InitializeAes</c>) is used;
    /// supplying an entropy switches to per-string entropy. A binary key cannot be expressed as an
    /// attribute argument — use the explicit <c>EncryptJsonValues(object, byte[])</c> overload for that.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class EncryptJsonValueAttribute : Attribute
    {
        public EncryptJsonValueAttribute()
        {
        }

        public EncryptJsonValueAttribute(string entropy)
        {
            Entropy = entropy;
        }

        /// <summary>
        /// Optional fixed entropy. When set, encryption uses string-entropy mode; otherwise the default key.
        /// </summary>
        public string Entropy { get; }
    }
}
