// Finds the InspectionModule implementations that happen to be installed.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaTools.Inspect.Editors
{
    internal static class ModuleRegistry
    {
        private static List<InspectionModule> _cache;

        /// <summary>
        /// Every module in the project, ordered. TypeCache is Unity's own
        /// precomputed index, so this costs nothing per scan and needs no
        /// assembly reference to the packages being found. The list is rebuilt
        /// on domain reload because static state does not survive one.
        /// </summary>
        internal static IReadOnlyList<InspectionModule> All()
        {
            if (_cache != null)
            {
                return _cache;
            }

            var modules = new List<InspectionModule>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<InspectionModule>())
            {
                if (type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }
                try
                {
                    modules.Add((InspectionModule)Activator.CreateInstance(type));
                }
                catch (Exception exception)
                {
                    // One broken module must not take the window down: the rest
                    // of the report is still worth having.
                    Debug.LogWarning(
                        $"[SabaTools] could not instantiate the inspection module {type.FullName}: " +
                        exception.Message);
                }
            }

            modules.Sort((a, b) => a.Order.CompareTo(b.Order));
            _cache = modules;
            return _cache;
        }
    }
}
