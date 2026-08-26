// The package's public entry point.
//
// Everything else in this assembly is internal: the window is one caller of
// this API, and the CI EditMode tests are another. Keeping the surface to this
// one class means a project that scripts its own pre-upload check has a stable
// thing to call, and the collectors stay free to change shape.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaTools.Inspect.Editors
{
    /// <summary>
    /// Runs a read-only inspection and returns the report. Nothing here
    /// modifies the scene, the assets or the selection.
    /// </summary>
    public static class InspectApi
    {
        /// <summary>Inspects one hierarchy.</summary>
        /// <param name="target">Root of the avatar or world object to scan.</param>
        /// <param name="mode">
        /// Which checklist to apply. <see cref="InspectMode.Auto"/> decides
        /// from the descriptors found on the target.
        /// </param>
        /// <param name="generatedAt">
        /// Timestamp text written into the report header. Pass an empty string
        /// to leave it out — useful when the output is compared between runs.
        /// </param>
        public static InspectionReport Inspect(
            GameObject target, InspectMode mode = InspectMode.Auto, string generatedAt = "")
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            return SceneScan.Run(new[] { target }, target.name, mode, generatedAt ?? string.Empty)
                .Report;
        }

        /// <summary>Inspects every root object of a scene.</summary>
        public static InspectionReport InspectScene(
            Scene scene, InspectMode mode = InspectMode.Auto, string generatedAt = "")
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("the scene is not valid", nameof(scene));
            }
            return SceneScan.Run(
                scene.GetRootGameObjects(), scene.name, mode, generatedAt ?? string.Empty).Report;
        }
    }
}
