using System;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// A single file attached to a mail sent through <see cref="IAppMailSender"/>.
    /// </summary>
    /// <remarks>
    /// The content is a <c>byte[]</c> on purpose: a stream would have to stay open for the whole lifetime of
    /// the send, which is the classic failure when the mail is handed to a background operation. Attachments
    /// travelling through this abstraction are small (receipts, reports, invitations) — materializing them is
    /// cheaper than owning their lifetime.
    /// </remarks>
    /// <param name="FileName">the name the recipient sees</param>
    /// <param name="Content">the raw content of the attachment</param>
    /// <param name="ContentType">the media-type of the attachment (e.g. <c>application/pdf</c>)</param>
    public sealed record MailAttachment(string FileName, byte[] Content, string ContentType)
    {
        /// <summary>
        /// The name the recipient sees.
        /// </summary>
        public string FileName { get; } = !string.IsNullOrWhiteSpace(FileName)
            ? FileName
            : throw new ArgumentException("A mail-attachment requires a file-name.", nameof(FileName));

        /// <summary>
        /// The raw content of the attachment.
        /// </summary>
        public byte[] Content { get; } = Content ?? throw new ArgumentNullException(nameof(Content));

        /// <summary>
        /// The media-type of the attachment. Falls back to <c>application/octet-stream</c> when not given.
        /// </summary>
        public string ContentType { get; } = !string.IsNullOrWhiteSpace(ContentType)
            ? ContentType
            : "application/octet-stream";
    }
}
