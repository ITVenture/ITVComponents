using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Configuration
{
    /// <summary>
    /// Contributes the global help system (topic tree, localized contents, resource library) to the
    /// system-configuration export and diff. Describe reads the current state into <see cref="HelpConfigMarkup"/>;
    /// Compare emits standard <see cref="Change"/> objects that the generic apply engine persists. References are
    /// resolved by natural name — topic slug, resource name, folder path — because ids differ per system.
    /// </summary>
    public class HelpConfigExtension : IConfigExtension
    {
        /// <summary>Separates the segments of a resource-folder path in the markup.</summary>
        private const char PathSeparator = '/';

        private readonly HelpConfigExportOptions options;

        public HelpConfigExtension(IOptions<HelpConfigExportOptions>? options = null)
        {
            this.options = options?.Value ?? new HelpConfigExportOptions();
        }

        /// <inheritdoc />
        public string SectionKey => HelpConfigMarkup.SectionName;

        /// <inheritdoc />
        public ConfigExtensionMarkup? Describe(DbContext db)
        {
            if (db is not IHelpSystemContext ctx)
            {
                return null;
            }

            var topics = ctx.HelpTopics.Include(t => t.Contents).AsNoTracking().ToList();
            var folders = ctx.HelpResourceFolders.AsNoTracking().ToList();
            var resources = ctx.HelpResources.Include(r => r.Files).AsNoTracking().ToList();

            var slugById = topics.ToDictionary(t => t.HelpTopicId, t => t.Slug);
            var folderPathById = BuildFolderPaths(folders);
            var blobs = ReadBlobs(ctx, resources.SelectMany(r => r.Files).ToList());

            return new HelpConfigMarkup
            {
                SectionKey = HelpConfigMarkup.SectionName,
                // Parents before children — the apply engine processes inserts in emission order, and a child
                // resolves its parent through the very rows inserted just before it.
                Topics = topics
                    .OrderBy(t => TopicDepth(t, topics))
                    .ThenBy(t => t.Slug, StringComparer.OrdinalIgnoreCase)
                    .Select(t => new HelpTopicMarkup
                    {
                        Slug = t.Slug,
                        ParentSlug = t.ParentId.HasValue && slugById.TryGetValue(t.ParentId.Value, out var ps) ? ps : null,
                        Kind = t.Kind,
                        SortOrder = t.SortOrder,
                        Icon = t.Icon,
                        IsPublished = t.IsPublished,
                        ShowInMenu = t.ShowInMenu,
                        Contents = t.Contents
                            .OrderBy(c => c.Culture, StringComparer.OrdinalIgnoreCase)
                            .Select(c => new HelpTopicContentMarkup { Culture = c.Culture, Title = c.Title, Body = c.Body })
                            .ToArray()
                    }).ToArray(),
                ResourceFolders = folderPathById.Values
                    .OrderBy(p => p.Count(c => c == PathSeparator))
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new HelpResourceFolderMarkup { Path = p })
                    .ToArray(),
                Resources = resources
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(r => new HelpResourceMarkup
                    {
                        Name = r.Name,
                        Description = r.Description,
                        Kind = r.Kind,
                        FolderPath = r.FolderId.HasValue && folderPathById.TryGetValue(r.FolderId.Value, out var fp) ? fp : null,
                        Files = r.Files
                            .OrderBy(f => f.Culture, StringComparer.OrdinalIgnoreCase)
                            .Select(f => ToFileMarkup(f, blobs))
                            .ToArray()
                    }).ToArray()
            };
        }

        /// <inheritdoc />
        public IEnumerable<Change> Compare(DbContext db, ConfigExtensionMarkup? current, ConfigExtensionMarkup? uploaded, IConfigChangeContext changes)
        {
            // Guarded here as well as in Describe: a file carrying this section can reach a system whose context
            // does not host the help tables at all, and the resulting changes would fail one by one on apply.
            if (db is not IHelpSystemContext)
            {
                return Array.Empty<Change>();
            }

            var cur = current as HelpConfigMarkup ?? new HelpConfigMarkup();
            var up = uploaded as HelpConfigMarkup ?? new HelpConfigMarkup();
            var result = new List<Change>();
            var unresolved = new List<string>();

            // Folders first (resources are filed into them), then the topic tree, then the resources.
            CompareFolders(cur.ResourceFolders ?? Array.Empty<HelpResourceFolderMarkup>(), up.ResourceFolders ?? Array.Empty<HelpResourceFolderMarkup>(), changes, result);
            CompareTopics(cur.Topics ?? Array.Empty<HelpTopicMarkup>(), up.Topics ?? Array.Empty<HelpTopicMarkup>(), changes, result);
            CompareResources(cur.Resources ?? Array.Empty<HelpResourceMarkup>(), up.Resources ?? Array.Empty<HelpResourceMarkup>(), changes, result, unresolved);
            ReportUnresolvedContents(unresolved, changes, result);
            return result;
        }

        // -- blobs -----------------------------------------------------------------------------------------

        /// <summary>Everything known about one stored file, as far as this system can see it.</summary>
        private sealed record BlobInfo(long Length, string? Hash, string? Content, string? OmittedReason);

        /// <summary>
        /// Reads the stored contents for the given file bindings from the built-in EF blob store, honouring the
        /// configured size limits. Sizes are queried before the bytes, so an oversized asset is recognised without
        /// being loaded. A binding with no row here lives in a host-specific <c>IHelpResourceStore</c> — its
        /// content is unreachable from the export, which is recorded rather than passed over.
        /// </summary>
        private Dictionary<string, BlobInfo> ReadBlobs(IHelpSystemContext ctx, List<HelpResourceFile> files)
        {
            var result = new Dictionary<string, BlobInfo>(StringComparer.Ordinal);
            var identifiers = files.Select(f => f.FileIdentifier).Where(i => !string.IsNullOrEmpty(i)).Distinct(StringComparer.Ordinal).ToList();
            if (identifiers.Count == 0)
            {
                return result;
            }

            if (!options.IncludeResourceContents)
            {
                foreach (var id in identifiers)
                {
                    result[id] = new BlobInfo(0, null, null, "resource contents are switched off for this export");
                }

                return result;
            }

            var sizes = ctx.HelpResourceBlobs
                .Where(b => identifiers.Contains(b.FileIdentifier))
                .Select(b => new { b.FileIdentifier, Length = (long)b.Content.Length })
                .AsNoTracking()
                .ToDictionary(b => b.FileIdentifier, b => b.Length, StringComparer.Ordinal);

            foreach (var id in identifiers.Where(i => !sizes.ContainsKey(i)))
            {
                result[id] = new BlobInfo(0, null, null, "the content is held in a host-specific resource store");
            }

            // Deterministic order, so the same export twice picks the same files when the total budget runs out.
            var carried = new List<string>();
            var budget = options.MaxTotalBytes;
            foreach (var entry in sizes.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                if (entry.Value > options.MaxFileBytes)
                {
                    result[entry.Key] = new BlobInfo(entry.Value, null, null,
                        $"the file exceeds the per-file limit of {Bytes(options.MaxFileBytes)}");
                }
                else if (entry.Value > budget)
                {
                    result[entry.Key] = new BlobInfo(entry.Value, null, null,
                        $"the export reached its overall content limit of {Bytes(options.MaxTotalBytes)}");
                }
                else
                {
                    budget -= entry.Value;
                    carried.Add(entry.Key);
                }
            }

            foreach (var blob in ctx.HelpResourceBlobs.Where(b => carried.Contains(b.FileIdentifier)).AsNoTracking())
            {
                var content = blob.Content ?? Array.Empty<byte>();
                result[blob.FileIdentifier] = new BlobInfo(content.Length, Hash(content), Convert.ToBase64String(content), null);
            }

            return result;
        }

        private static HelpResourceFileMarkup ToFileMarkup(HelpResourceFile file, Dictionary<string, BlobInfo> blobs)
        {
            blobs.TryGetValue(file.FileIdentifier ?? string.Empty, out var blob);
            return new HelpResourceFileMarkup
            {
                Culture = file.Culture,
                ContentType = file.ContentType,
                OriginalName = file.OriginalName,
                FileIdentifier = file.FileIdentifier,
                ContentHash = blob?.Hash,
                ContentLength = blob?.Length ?? 0,
                Content = blob?.Content,
                ContentOmittedReason = blob?.OmittedReason
            };
        }

        private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        private static string Bytes(long value) => value >= 1024 * 1024
            ? $"{value / (1024d * 1024d):0.#} MB"
            : $"{Math.Max(1, value / 1024)} KB";

        // -- folders ---------------------------------------------------------------------------------------

        /// <summary>
        /// Folders are created when missing and otherwise left alone — never updated, never deleted. They carry
        /// nothing but a name and a parent, so "renamed" and "moved" are indistinguishable from "a different
        /// folder" over a path key; and deleting one would silently move another system's resources to the root.
        /// Being pure organisation (resources resolve by their flat global name), that is the harmless side to
        /// err on.
        /// </summary>
        private static void CompareFolders(HelpResourceFolderMarkup[] cur, HelpResourceFolderMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            var existing = new HashSet<string>(cur.Where(f => f?.Path != null).Select(f => f.Path!), StringComparer.OrdinalIgnoreCase);

            foreach (var path in up.Where(f => !string.IsNullOrWhiteSpace(f?.Path))
                         .Select(f => f.Path!)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(p => p.Count(c => c == PathSeparator))
                         .ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (existing.Contains(path) || !IsExpressionSafe(path))
                {
                    continue;
                }

                var segments = path.Split(PathSeparator);
                var c = new Change { ChangeType = ChangeType.Insert, EntityName = "HelpResourceFolders", Apply = true };
                c.Details.Add(ch.MakeDetail("Name", segments[^1]));
                if (segments.Length > 1)
                {
                    var parentSegments = segments[..^1];
                    c.Details.Add(ch.MakeDetail("Parent", parentSegments[^1],
                        ch.MakeLinqAssign("Parent", "HelpResourceFolders", "Name", FolderChainWhere(parentSegments))));
                }

                result.Add(c);
                existing.Add(path);
            }
        }

        // -- topics ----------------------------------------------------------------------------------------

        private static void CompareTopics(HelpTopicMarkup[] cur, HelpTopicMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            var curBySlug = ToDictionary(cur, t => t.Slug);
            var upBySlug = ToDictionary(up, t => t.Slug);

            // Inserts: parents before children, so a child finds its parent among the rows just added.
            foreach (var upT in up.Where(t => t?.Slug != null && !curBySlug.ContainsKey(t.Slug!))
                         .OrderBy(t => MarkupDepth(t, upBySlug))
                         .ThenBy(t => t.Slug, StringComparer.OrdinalIgnoreCase))
            {
                var c = new Change { ChangeType = ChangeType.Insert, EntityName = "HelpTopics", Apply = true };
                c.Details.Add(ch.MakeDetail("Slug", upT.Slug));
                c.Details.Add(ch.MakeDetail("Kind", upT.Kind.ToString()));
                c.Details.Add(ch.MakeDetail("SortOrder", upT.SortOrder.ToString(CultureInfo.InvariantCulture)));
                c.Details.Add(ch.MakeDetail("Icon", upT.Icon ?? string.Empty));
                c.Details.Add(ch.MakeDetail("IsPublished", upT.IsPublished.ToString(), BoolAssign(nameof(HelpTopic.IsPublished))));
                c.Details.Add(ch.MakeDetail("ShowInMenu", upT.ShowInMenu.ToString(), BoolAssign(nameof(HelpTopic.ShowInMenu))));
                if (!string.IsNullOrEmpty(upT.ParentSlug))
                {
                    c.Details.Add(ch.MakeDetail("Parent", upT.ParentSlug, ch.MakeLinqAssign("Parent", "HelpTopics", "Slug")));
                }

                result.Add(c);
                CompareTopicContents(upT.Slug!, Array.Empty<HelpTopicContentMarkup>(), upT.Contents ?? Array.Empty<HelpTopicContentMarkup>(), ch, result);
            }

            // Updates.
            foreach (var upT in up.Where(t => t?.Slug != null && curBySlug.ContainsKey(t.Slug!))
                         .OrderBy(t => t.Slug, StringComparer.OrdinalIgnoreCase))
            {
                var curT = curBySlug[upT.Slug!];
                var c = new Change
                {
                    ChangeType = ChangeType.Update, EntityName = "HelpTopics", Apply = true,
                    Key = Key(("Slug", curT.Slug!))
                };

                if (upT.Kind != curT.Kind)
                {
                    c.Details.Add(ch.MakeDetail("Kind", upT.Kind.ToString(), currentValue: curT.Kind.ToString()));
                }

                if (upT.SortOrder != curT.SortOrder)
                {
                    c.Details.Add(ch.MakeDetail("SortOrder", upT.SortOrder.ToString(CultureInfo.InvariantCulture),
                        currentValue: curT.SortOrder.ToString(CultureInfo.InvariantCulture)));
                }

                if (TextChanged(upT.Icon, curT.Icon))
                {
                    c.Details.Add(ch.MakeDetail("Icon", upT.Icon ?? string.Empty, currentValue: curT.Icon));
                }

                if (upT.IsPublished != curT.IsPublished)
                {
                    c.Details.Add(ch.MakeDetail("IsPublished", upT.IsPublished.ToString(), BoolAssign(nameof(HelpTopic.IsPublished)), curT.IsPublished.ToString()));
                }

                if (upT.ShowInMenu != curT.ShowInMenu)
                {
                    c.Details.Add(ch.MakeDetail("ShowInMenu", upT.ShowInMenu.ToString(), BoolAssign(nameof(HelpTopic.ShowInMenu)), curT.ShowInMenu.ToString()));
                }

                if (!string.Equals(upT.ParentSlug, curT.ParentSlug, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(upT.ParentSlug))
                    {
                        c.Details.Add(ch.MakeDetail("Parent", upT.ParentSlug, ch.MakeLinqAssign("Parent", "HelpTopics", "Slug"), curT.ParentSlug));
                    }
                    else
                    {
                        // Moved to the root: clearing the scalar key is what actually detaches the parent.
                        c.Details.Add(ch.MakeDetail("ParentId", null, $"Entity.{nameof(HelpTopic.ParentId)}=null", curT.ParentSlug));
                    }
                }

                if (c.Details.Count != 0)
                {
                    result.Add(c);
                }

                CompareTopicContents(curT.Slug!, curT.Contents ?? Array.Empty<HelpTopicContentMarkup>(), upT.Contents ?? Array.Empty<HelpTopicContentMarkup>(), ch, result);
            }

            // Deletes: deepest first, so no topic is removed while it still has children pointing at it.
            var doomed = cur.Where(t => t?.Slug != null && !upBySlug.ContainsKey(t.Slug!))
                .OrderByDescending(t => MarkupDepth(t, curBySlug))
                .ThenBy(t => t.Slug, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var i = 0; i < doomed.Count; i++)
            {
                result.Add(new Change
                {
                    ChangeType = ChangeType.Delete, EntityName = "HelpTopics", Apply = true,
                    DeletePriority = TopicDeletePriority + i,
                    Key = Key(("Slug", doomed[i].Slug!))
                });
            }
        }

        private static void CompareTopicContents(string topicSlug, HelpTopicContentMarkup[] cur, HelpTopicContentMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            foreach (var (curC, upC) in JoinBy(cur, up, c => c.Culture))
            {
                if (curC != null && upC == null)
                {
                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "HelpTopicContents", Apply = true,
                        DeletePriority = ContentDeletePriority,
                        Key = Key(("Culture", curC.Culture!), ("Topic", topicSlug)),
                        KeyExpression = new Dictionary<string, string> { { "Topic", ch.MakeLinqQuery("HelpTopics", "Slug") } }
                    });
                }
                else if (curC == null && upC != null)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "HelpTopicContents", Apply = true };
                    c.Details.Add(ch.MakeDetail("Culture", upC.Culture));
                    c.Details.Add(ch.MakeDetail("Title", upC.Title ?? string.Empty));
                    c.Details.Add(ch.MakeDetail("Body", upC.Body ?? string.Empty, multiline: true));
                    c.Details.Add(ch.MakeDetail("Topic", topicSlug, ch.MakeLinqAssign("Topic", "HelpTopics", "Slug")));
                    result.Add(c);
                }
                else if (curC != null && upC != null)
                {
                    var c = new Change
                    {
                        ChangeType = ChangeType.Update, EntityName = "HelpTopicContents", Apply = true,
                        Key = Key(("Culture", curC.Culture!), ("Topic", topicSlug)),
                        KeyExpression = new Dictionary<string, string> { { "Topic", ch.MakeLinqQuery("HelpTopics", "Slug") } }
                    };

                    if (TextChanged(upC.Title, curC.Title))
                    {
                        c.Details.Add(ch.MakeDetail("Title", upC.Title ?? string.Empty, currentValue: curC.Title));
                    }

                    if (!string.Equals(upC.Body ?? string.Empty, curC.Body ?? string.Empty, StringComparison.Ordinal))
                    {
                        c.Details.Add(ch.MakeDetail("Body", upC.Body ?? string.Empty, currentValue: curC.Body, multiline: true));
                    }

                    if (c.Details.Count != 0)
                    {
                        result.Add(c);
                    }
                }
            }
        }

        // -- resources -------------------------------------------------------------------------------------

        private static void CompareResources(HelpResourceMarkup[] cur, HelpResourceMarkup[] up, IConfigChangeContext ch, List<Change> result, List<string> unresolved)
        {
            foreach (var (curR, upR) in JoinBy(cur, up, r => r.Name))
            {
                if (curR != null && upR == null)
                {
                    // The file bindings cascade with the resource; the blobs do not (the identifier is a handle,
                    // not a foreign key), so they are removed explicitly to avoid leaving orphans behind.
                    foreach (var file in curR.Files ?? Array.Empty<HelpResourceFileMarkup>())
                    {
                        AddBlobDelete(file, result);
                    }

                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "HelpResources", Apply = true,
                        DeletePriority = ResourceDeletePriority,
                        Key = Key(("Name", curR.Name!))
                    });
                }
                else if (curR == null && upR != null)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "HelpResources", Apply = true };
                    c.Details.Add(ch.MakeDetail("Name", upR.Name));
                    c.Details.Add(ch.MakeDetail("Description", upR.Description ?? string.Empty));
                    c.Details.Add(ch.MakeDetail("Kind", upR.Kind.ToString()));
                    AddFolderAssignment(c, ch, upR.FolderPath, null);
                    result.Add(c);
                    CompareResourceFiles(upR.Name!, Array.Empty<HelpResourceFileMarkup>(), upR.Files ?? Array.Empty<HelpResourceFileMarkup>(), ch, result, unresolved);
                }
                else if (curR != null && upR != null)
                {
                    var c = new Change
                    {
                        ChangeType = ChangeType.Update, EntityName = "HelpResources", Apply = true,
                        Key = Key(("Name", curR.Name!))
                    };

                    if (TextChanged(upR.Description, curR.Description))
                    {
                        c.Details.Add(ch.MakeDetail("Description", upR.Description ?? string.Empty, currentValue: curR.Description));
                    }

                    if (upR.Kind != curR.Kind)
                    {
                        c.Details.Add(ch.MakeDetail("Kind", upR.Kind.ToString(), currentValue: curR.Kind.ToString()));
                    }

                    if (!string.Equals(upR.FolderPath, curR.FolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        AddFolderAssignment(c, ch, upR.FolderPath, curR.FolderPath);
                    }

                    if (c.Details.Count != 0)
                    {
                        result.Add(c);
                    }

                    CompareResourceFiles(curR.Name!, curR.Files ?? Array.Empty<HelpResourceFileMarkup>(), upR.Files ?? Array.Empty<HelpResourceFileMarkup>(), ch, result, unresolved);
                }
            }
        }

        /// <summary>
        /// Compares the per-culture file bindings of one resource. A binding is only created when the export
        /// actually carries the bytes — the storage handle it holds is meaningless on this system, so a binding
        /// without content would resolve to nothing. What cannot be created (or updated) is collected for a single
        /// note in the diff instead of failing silently.
        /// </summary>
        private static void CompareResourceFiles(string resourceName, HelpResourceFileMarkup[] cur, HelpResourceFileMarkup[] up, IConfigChangeContext ch, List<Change> result, List<string> unresolved)
        {
            foreach (var (curF, upF) in JoinBy(cur, up, f => f.Culture))
            {
                if (curF != null && upF == null)
                {
                    AddBlobDelete(curF, result);
                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "HelpResourceFiles", Apply = true,
                        DeletePriority = FileDeletePriority,
                        Key = Key(("Culture", curF.Culture!), ("Resource", resourceName)),
                        KeyExpression = new Dictionary<string, string> { { "Resource", ch.MakeLinqQuery("HelpResources", "Name") } }
                    });
                }
                else if (curF == null && upF != null)
                {
                    if (upF.Content == null)
                    {
                        unresolved.Add($"{resourceName} ({upF.Culture}) — not created: {upF.ContentOmittedReason ?? "the export carries no content for it"}");
                        continue;
                    }

                    // A fresh handle: the identifier is opaque and owned by each system's own store, so the one
                    // from the export is deliberately not reused.
                    var identifier = Guid.NewGuid().ToString();
                    result.Add(BuildBlobInsert(identifier, upF, ch));

                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "HelpResourceFiles", Apply = true };
                    c.Details.Add(ch.MakeDetail("Culture", upF.Culture));
                    c.Details.Add(ch.MakeDetail("FileIdentifier", identifier));
                    c.Details.Add(ch.MakeDetail("ContentType", upF.ContentType ?? string.Empty));
                    c.Details.Add(ch.MakeDetail("OriginalName", upF.OriginalName ?? string.Empty));
                    c.Details.Add(ch.MakeDetail("Resource", resourceName, ch.MakeLinqAssign("Resource", "HelpResources", "Name")));
                    result.Add(c);
                }
                else if (curF != null && upF != null)
                {
                    var contentChanged = upF.ContentHash != null && curF.ContentHash != null
                                         && !string.Equals(upF.ContentHash, curF.ContentHash, StringComparison.OrdinalIgnoreCase);

                    if (contentChanged && upF.Content == null)
                    {
                        unresolved.Add($"{resourceName} ({upF.Culture}) — not updated: {upF.ContentOmittedReason ?? "the export carries no content for it"}");
                    }
                    else if (contentChanged && !string.IsNullOrEmpty(curF.FileIdentifier))
                    {
                        // The blob is addressed by THIS system's handle, taken from the local description.
                        var blob = new Change
                        {
                            ChangeType = ChangeType.Update, EntityName = "HelpResourceBlobs", Apply = true,
                            Key = Key(("FileIdentifier", curF.FileIdentifier!))
                        };
                        blob.Details.Add(ContentDetail(upF, ch, curF));
                        blob.Details.Add(ch.MakeDetail("ContentType", upF.ContentType ?? string.Empty, currentValue: curF.ContentType));
                        blob.Details.Add(ch.MakeDetail("DownloadName", upF.OriginalName ?? string.Empty, currentValue: curF.OriginalName));
                        result.Add(blob);
                    }

                    var c = new Change
                    {
                        ChangeType = ChangeType.Update, EntityName = "HelpResourceFiles", Apply = true,
                        Key = Key(("Culture", curF.Culture!), ("Resource", resourceName)),
                        KeyExpression = new Dictionary<string, string> { { "Resource", ch.MakeLinqQuery("HelpResources", "Name") } }
                    };

                    if (TextChanged(upF.ContentType, curF.ContentType))
                    {
                        c.Details.Add(ch.MakeDetail("ContentType", upF.ContentType ?? string.Empty, currentValue: curF.ContentType));
                    }

                    if (TextChanged(upF.OriginalName, curF.OriginalName))
                    {
                        c.Details.Add(ch.MakeDetail("OriginalName", upF.OriginalName ?? string.Empty, currentValue: curF.OriginalName));
                    }

                    if (c.Details.Count != 0)
                    {
                        result.Add(c);
                    }
                }
            }
        }

        private static Change BuildBlobInsert(string identifier, HelpResourceFileMarkup file, IConfigChangeContext ch)
        {
            var blob = new Change { ChangeType = ChangeType.Insert, EntityName = "HelpResourceBlobs", Apply = true };
            blob.Details.Add(ch.MakeDetail("FileIdentifier", identifier));
            blob.Details.Add(ch.MakeDetail("ContentType", file.ContentType ?? string.Empty));
            blob.Details.Add(ch.MakeDetail("DownloadName", file.OriginalName ?? string.Empty));
            blob.Details.Add(ContentDetail(file, ch, null));
            blob.Details.Add(ch.MakeDetail("Created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            return blob;
        }

        /// <summary>
        /// The content detail: base64 in the payload, a readable summary in the dialog, and locked against
        /// editing — a single keystroke in a base64 field would destroy the file.
        /// </summary>
        private static ChangeDetail ContentDetail(HelpResourceFileMarkup file, IConfigChangeContext ch, HelpResourceFileMarkup? currentFile)
        {
            var detail = ch.MakeDetail("Content", file.Content, "Entity.Content='System.Convert'.FromBase64String(NewValueRaw)");
            detail.DisplayValue = FileSummary(file);
            detail.DisplayCurrentValue = currentFile != null ? FileSummary(currentFile) : null;
            detail.ReadOnly = true;
            return detail;
        }

        private static string FileSummary(HelpResourceFileMarkup file)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(file.OriginalName))
            {
                parts.Add(file.OriginalName!);
            }

            if (file.ContentLength > 0)
            {
                parts.Add(Bytes(file.ContentLength));
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                parts.Add(file.ContentType!);
            }

            if (!string.IsNullOrWhiteSpace(file.ContentHash))
            {
                parts.Add($"sha256 {file.ContentHash![..Math.Min(12, file.ContentHash.Length)]}…");
            }

            return parts.Count != 0 ? string.Join(" · ", parts) : "(no file details)";
        }

        private static void AddBlobDelete(HelpResourceFileMarkup file, List<Change> result)
        {
            if (string.IsNullOrEmpty(file.FileIdentifier))
            {
                return;
            }

            result.Add(new Change
            {
                ChangeType = ChangeType.Delete, EntityName = "HelpResourceBlobs", Apply = true,
                DeletePriority = BlobDeletePriority,
                Key = Key(("FileIdentifier", file.FileIdentifier!))
            });
        }

        /// <summary>Adds the folder assignment (or its removal) to a resource change.</summary>
        private static void AddFolderAssignment(Change c, IConfigChangeContext ch, string? folderPath, string? currentPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    c.Details.Add(ch.MakeDetail("FolderId", null, $"Entity.{nameof(HelpResource.FolderId)}=null", currentPath));
                }

                return;
            }

            if (!IsExpressionSafe(folderPath))
            {
                return;
            }

            var segments = folderPath.Split(PathSeparator);
            c.Details.Add(ch.MakeDetail("Folder", segments[^1],
                ch.MakeLinqAssign("Folder", "HelpResourceFolders", "Name", FolderChainWhere(segments)), currentPath));
        }

        /// <summary>
        /// One collected note for every file the export described but could not transfer — more use than a row
        /// per file, and better than letting the gap pass unmentioned.
        /// </summary>
        private static void ReportUnresolvedContents(List<string> unresolved, IConfigChangeContext ch, List<Change> result)
        {
            if (unresolved.Count == 0)
            {
                return;
            }

            var c = new Change
            {
                ChangeType = ChangeType.Warning,
                EntityName = $"{unresolved.Count} help resource file(s) without content"
            };
            c.Details.Add(ch.MakeDetail("Consequence",
                "Upload these files through the help resource administration, or raise the content limits of the exporting system and export again.",
                apply: false));
            c.Details.Add(ch.MakeDetail("Affected",
                string.Join(Environment.NewLine, unresolved.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)),
                apply: false, multiline: true));
            result.Add(c);
        }

        // -- helpers ---------------------------------------------------------------------------------------

        // Delete order across the whole section: blobs and bindings first, then contents and resources, then the
        // topic tree from its leaves upwards (each doomed topic gets its own slot above TopicDeletePriority).
        private const int BlobDeletePriority = 0;
        private const int FileDeletePriority = 1;
        private const int ContentDeletePriority = 2;
        private const int ResourceDeletePriority = 3;
        private const int TopicDeletePriority = 10;

        /// <summary>Assignment expression for a bool — the generic value conversion does not cover them.</summary>
        private static string BoolAssign(string property) => $"Entity.{property}=(NewValueRaw==\"True\")";

        /// <summary>
        /// Builds the ancestor condition for a folder looked up by its leaf name: every level above the leaf is
        /// pinned by name and the chain is closed with a null parent, so an identically named folder elsewhere in
        /// the tree cannot match. The null checks matter — an entity whose parent is not loaded yields false here
        /// and falls through to the database lookup instead of failing the expression.
        /// </summary>
        private static string FolderChainWhere(string[] segments)
        {
            var conditions = new List<string>();
            var prefix = "n.Parent";
            for (var i = segments.Length - 2; i >= 0; i--)
            {
                conditions.Add($"{prefix} != null");
                conditions.Add($"{prefix}.Name == \"{segments[i]}\"");
                prefix += ".Parent";
            }

            conditions.Add($"{prefix} == null");
            return string.Join(" && ", conditions);
        }

        /// <summary>
        /// Folder names travel inside a generated lookup expression. A name containing a quote would break that
        /// expression, so such a folder is skipped rather than allowed to produce something unparseable.
        /// </summary>
        private static bool IsExpressionSafe(string? path) => path != null && !path.Contains('"');

        /// <summary>Depth of a topic in the live tree (root = 0), guarded against a cyclic parent chain.</summary>
        private static int TopicDepth(HelpTopic topic, List<HelpTopic> all)
        {
            var byId = all.ToDictionary(t => t.HelpTopicId);
            var depth = 0;
            var cursor = topic;
            while (cursor?.ParentId != null && byId.TryGetValue(cursor.ParentId.Value, out var parent) && depth <= all.Count)
            {
                depth++;
                cursor = parent;
            }

            return depth;
        }

        /// <summary>Depth of a topic in a markup tree (root = 0), guarded against a cyclic parent chain.</summary>
        private static int MarkupDepth(HelpTopicMarkup topic, Dictionary<string, HelpTopicMarkup> bySlug)
        {
            var depth = 0;
            var cursor = topic;
            while (!string.IsNullOrEmpty(cursor?.ParentSlug) && bySlug.TryGetValue(cursor.ParentSlug!, out var parent) && depth <= bySlug.Count)
            {
                depth++;
                cursor = parent;
            }

            return depth;
        }

        /// <summary>Full path of every folder, keyed by id; cyclic or orphaned parents fall back to the bare name.</summary>
        private static Dictionary<int, string> BuildFolderPaths(List<HelpResourceFolder> folders)
        {
            var byId = folders.ToDictionary(f => f.HelpResourceFolderId);
            var result = new Dictionary<int, string>();
            foreach (var folder in folders)
            {
                var segments = new List<string>();
                var cursor = folder;
                while (cursor != null && segments.Count <= folders.Count)
                {
                    segments.Insert(0, cursor.Name);
                    cursor = cursor.ParentId.HasValue && byId.TryGetValue(cursor.ParentId.Value, out var parent) ? parent : null;
                }

                result[folder.HelpResourceFolderId] = string.Join(PathSeparator, segments);
            }

            return result;
        }

        private static Dictionary<string, T> ToDictionary<T>(IEnumerable<T>? items, Func<T, string?> key) where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                var k = item != null ? key(item) : null;
                if (k != null)
                {
                    result[k] = item!;
                }
            }

            return result;
        }

        private static Dictionary<string, string> Key(params (string Name, string Value)[] parts)
            => parts.ToDictionary(p => p.Name, p => p.Value);

        private static bool TextChanged(string? up, string? cur)
            => (up != cur && !string.IsNullOrWhiteSpace(up) && !string.IsNullOrWhiteSpace(cur))
               || (string.IsNullOrWhiteSpace(up) != string.IsNullOrWhiteSpace(cur));

        private static IEnumerable<(T? Cur, T? Up)> JoinBy<T>(IEnumerable<T>? cur, IEnumerable<T>? up, Func<T, string?> key) where T : class
        {
            var curBy = ToDictionary(cur, key);
            var upBy = ToDictionary(up, key);
            var all = new HashSet<string>(curBy.Keys, StringComparer.OrdinalIgnoreCase);
            all.UnionWith(upBy.Keys);
            foreach (var k in all.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                yield return (curBy.TryGetValue(k, out var cv) ? cv : null, upBy.TryGetValue(k, out var uv) ? uv : null);
            }
        }
    }
}
