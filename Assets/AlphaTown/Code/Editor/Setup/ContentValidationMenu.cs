using System.Collections.Generic;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Validation;
using UnityEditor;
using UnityEngine;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Runs <see cref="ContentValidator"/> over the project's database and prints what it finds.
    ///
    /// The validator itself lives in Data so a test can run it with no Unity at all. This is only
    /// the part that has to know where the asset is and how a person wants to read the answer:
    /// one line per issue, errors first, and each subject spelled exactly as the asset is named so
    /// the project search box finds it.
    /// </summary>
    internal static class ContentValidationMenu
    {
        const string DatabasePath = "Assets/AlphaTown/Content/GameDatabase.asset";

        [MenuItem("AlphaTown/Content/Validate Content", false, 24)]
        internal static void Validate()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("[AlphaTown] No database at " + DatabasePath +
                               ". Run AlphaTown ▸ Content ▸ Build Sample Content first.");

                return;
            }

            // The asset caches its indexes, and a generate run in this same session will have left
            // stale ones behind. Validating yesterday's graph is worse than not validating.
            database.Reindex();
            Report(database);
        }

        /// <summary>
        /// Logs every issue and returns how many were errors, so a caller can decide whether the
        /// content it just wrote is worth pointing the game at.
        /// </summary>
        internal static int Report(IGameDatabase database)
        {
            var issues = ContentValidator.Validate(database);
            if (issues.Count == 0)
            {
                Debug.Log("[AlphaTown] Content validated: nothing to report.");
                return 0;
            }

            var errors = 0;
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentIssueSeverity.Error) errors++;
            }

            // One log entry rather than one per issue: forty separate warnings scroll the console
            // clean of whatever the author was actually looking at.
            Debug.Log("[AlphaTown] Content validation: " + errors + " error(s), " +
                      (issues.Count - errors) + " warning(s).\n" + Format(issues));

            return errors;
        }

        static string Format(List<ContentIssue> issues)
        {
            var text = new System.Text.StringBuilder();

            for (var severity = ContentIssueSeverity.Error; ; severity = ContentIssueSeverity.Warning)
            {
                for (var i = 0; i < issues.Count; i++)
                {
                    if (issues[i].Severity != severity) continue;
                    text.Append("  ").Append(issues[i]).Append('\n');
                }

                if (severity == ContentIssueSeverity.Warning) break;
            }

            return text.ToString();
        }
    }
}
