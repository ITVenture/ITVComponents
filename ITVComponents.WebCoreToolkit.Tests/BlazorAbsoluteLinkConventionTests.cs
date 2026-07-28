using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Source-scanning regression guard for the link convention described on <see cref="TenantUrlGuard"/>:
    /// Blazor view packages must emit navigation targets <em>relative</em> (no leading slash) so they resolve
    /// against <c>&lt;base href="/{tenant}/"&gt;</c>.
    /// <para>
    /// Why a source scan and not a runtime test: the failure mode is invisible to every runtime test in this
    /// repository. A root-absolute <c>&lt;a href="/Foo"&gt;</c> resolves outside the base-URI space, so
    /// Blazor's JS does not intercept the click — the browser leaves the circuit, and neither
    /// <see cref="TenantUrlGuard"/> (already gone) nor any component code ever sees the navigation. It
    /// surfaces only as a 404 from <see cref="TenantPathPrefixMiddleware"/> in a running, logged-in host.
    /// That is what happened in PRE141; this test is the cheapest place to catch the next occurrence.
    /// </para>
    /// </summary>
    [TestClass]
    public class BlazorAbsoluteLinkConventionTests
    {
        private static readonly Regex NavigateToAbsolute = new Regex(@"\bNavigateTo\(\s*\$?""(/[^""]*)""", RegexOptions.Compiled);
        private static readonly Regex HrefAbsolute = new Regex(@"\bhref\s*=\s*""(/[^""]*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [TestMethod]
        public void Blazor_View_Packages_Emit_Relative_Navigation_Targets()
        {
            var root = FindRepositoryRoot();
            if (root == null)
            {
                Assert.Inconclusive("Repository root (ITVComponents.sln) not found — scan skipped.");
                return;
            }

            var exclusions = new ScopedPermissionScopeOptions().AuthPathExclusions;
            var violations = new List<string>();
            var scanned = 0;

            foreach (var file in BlazorSourceFiles(root))
            {
                scanned++;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (IsComment(line) || line.IndexOf("<base ", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    foreach (var target in Targets(line))
                    {
                        if (IsTenantNeutral(target, exclusions))
                        {
                            continue;
                        }

                        violations.Add($"{Path.GetRelativePath(root, file)}({i + 1}): '{target}'");
                    }
                }
            }

            // Ohne diese Zeile waere ein gruener Lauf nicht unterscheidbar von "gar nichts gefunden" — genau
            // die stille Variante, die man nie bemerkt (verschobene Projektordner, geaenderte Namenskonvention).
            Assert.IsTrue(scanned > 100, $"Scan lief ins Leere: nur {scanned} Blazor-Quelldateien unter '{root}' gefunden.");

            Assert.AreEqual(0, violations.Count,
                "Root-absolute navigation targets in Blazor view packages. They escape the tenant prefix: the "
                + "browser resolves them outside <base href=\"/{tenant}/\">, never enters the circuit, and "
                + "TenantPathPrefixMiddleware answers 404. Emit them without the leading slash — or, if the path "
                + "really is tenant-neutral, add it to ScopedPermissionScopeOptions.AuthPathExclusions."
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        private static IEnumerable<string> Targets(string line)
        {
            foreach (Match m in NavigateToAbsolute.Matches(line))
            {
                yield return m.Groups[1].Value;
            }

            foreach (Match m in HrefAbsolute.Matches(line))
            {
                yield return m.Groups[1].Value;
            }
        }

        /// <summary>
        /// "/" itself is legitimate: <see cref="TenantPathPrefixMiddleware"/> redirects the root to the user's
        /// default eligible scope. Blazor infrastructure paths ("/_blazor", "/_content/...") are served before
        /// the tenant middleware and must stay absolute.
        /// </summary>
        private static bool IsTenantNeutral(string target, IList<string> exclusions)
            => target == "/"
               || target.StartsWith("/_", StringComparison.Ordinal)
               || exclusions.Any(x => !string.IsNullOrEmpty(x) && target.StartsWith(x, StringComparison.OrdinalIgnoreCase));

        private static bool IsComment(string line)
        {
            var t = line.TrimStart();
            return t.StartsWith("//", StringComparison.Ordinal)
                   || t.StartsWith("@*", StringComparison.Ordinal)
                   || t.StartsWith("*", StringComparison.Ordinal)
                   || t.StartsWith("<!--", StringComparison.Ordinal);
        }

        private static IEnumerable<string> BlazorSourceFiles(string root)
            => from dir in Directory.EnumerateDirectories(root)
               where Path.GetFileName(dir).IndexOf("Blazor", StringComparison.OrdinalIgnoreCase) >= 0
               from file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
               let ext = Path.GetExtension(file)
               where string.Equals(ext, ".razor", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase)
               where !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                     && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               select file;

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ITVComponents.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName;
        }
    }
}
