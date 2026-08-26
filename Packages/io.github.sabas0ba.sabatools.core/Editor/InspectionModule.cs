// The extension point that keeps the VRChat SDK out of this package.
//
// com.vrchat.avatars and com.vrchat.worlds cannot both be installed in one
// project, so a package that declared either as a VPM dependency would decide
// the project type for its user. This package declares neither. The SDK-aware
// checks live in sabatools.avatar and sabatools.world, which reference this
// assembly and are discovered at runtime -- the arrow points this way only, and
// nothing here knows those assemblies exist.
using System.Collections.Generic;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    /// <summary>
    /// What a module is handed for one inspection run. Adding to it is the
    /// only way a module contributes; it cannot reach the collectors, and it
    /// is given no means of writing to the scene.
    /// </summary>
    public sealed class InspectionContext
    {
        private readonly Dictionary<string, Object> _locations;

        internal InspectionContext(
            GameObject[] roots, InspectionReport report, Dictionary<string, Object> locations)
        {
            Roots = roots;
            Report = report;
            _locations = locations;
        }

        /// <summary>The roots being inspected. Read them; do not modify them.</summary>
        public GameObject[] Roots { get; }

        /// <summary>The report being built. <see cref="InspectionReport.Snapshot"/>
        /// already holds everything the core collectors counted.</summary>
        public InspectionReport Report { get; }

        /// <summary>The numbers the core collectors produced.</summary>
        public StatsSnapshot Stats => Report.Snapshot;

        /// <summary>Adds a row to the statistics table.</summary>
        public void AddRow(string group, string label, string value)
        {
            Report.Rows.Add(new StatRow(group, label, value));
        }

        /// <summary>
        /// Adds a finding. Passing <paramref name="location"/> lets the window
        /// offer a Ping button for it.
        /// </summary>
        public void Add(
            InspectionSeverity severity, string category, string message, Object location = null)
        {
            string path = location is GameObject gameObject
                ? ScanUtil.HierarchyPath(gameObject.transform)
                : location is Component component
                    ? ScanUtil.HierarchyPath(component.transform)
                    : string.Empty;

            Report.Items.Add(new InspectionItem(category, severity, message, path));

            if (path.Length > 0 && location != null && !_locations.ContainsKey(path))
            {
                _locations.Add(path, location);
            }
        }
    }

    /// <summary>
    /// A set of checks for one kind of VRChat content. Derive from this in a
    /// package that may depend on a VRChat SDK; the core window finds the type
    /// through <c>TypeCache</c> and never references the assembly it is in.
    /// <para>
    /// Implementations must stay non-destructive. That is the promise the whole
    /// tool is built on, and a module breaking it breaks it for everything.
    /// </para>
    /// </summary>
    public abstract class InspectionModule
    {
        /// <summary>Shown in the report so it is clear which module spoke.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Which mode this module handles. Never <see cref="InspectMode.Auto"/>.</summary>
        public abstract InspectMode Mode { get; }

        /// <summary>
        /// True when this module recognises the roots as its kind of content --
        /// an avatar module looks for an avatar descriptor. Auto mode picks the
        /// first module that says yes.
        /// </summary>
        public abstract bool Detect(GameObject[] roots);

        /// <summary>Runs the module's checks. Called only when the resolved
        /// mode matches <see cref="Mode"/>.</summary>
        public abstract void Inspect(InspectionContext context);

        /// <summary>
        /// Lower runs first, and decides which module wins in Auto mode when
        /// more than one recognises the content.
        /// </summary>
        public virtual int Order => 0;
    }
}
