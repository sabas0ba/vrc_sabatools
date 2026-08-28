// EditMode tests for the world module, run against the real Worlds SDK.
//
// The offline harness in .github/verify/offline covers WorldLimits, which is
// arithmetic. What it cannot reach is everything this file exercises: that the
// SDK field names the module reads still exist, that TypeCache finds the
// module from the core assembly without a reference, and that Auto mode
// resolves to World because a real descriptor was seen.
using NUnit.Framework;
using SabaTools.Inspect;
using SabaTools.Inspect.Editors;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaTools.Inspect.World.SdkTests
{
    public class WorldModuleTests
    {
        private GameObject _world;
        private GameObject _spawn;
        private GameObject _floor;

        [SetUp]
        public void CreateWorld()
        {
            _world = new GameObject("VRCWorld");

            _floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floor.transform.SetParent(_world.transform);
            _floor.transform.position = Vector3.zero;

            _spawn = new GameObject("Spawn");
            _spawn.transform.SetParent(_world.transform);

            var descriptor = _world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { _spawn.transform };
            descriptor.RespawnHeightY = -100f;
        }

        [TearDown]
        public void DestroyWorld()
        {
            if (_world != null)
            {
                Object.DestroyImmediate(_world);
            }
        }

        [Test]
        public void AutoModeResolvesToWorldFromARealDescriptor()
        {
            InspectionReport report = InspectApi.Inspect(_world);
            Assert.AreEqual(InspectMode.World, report.Mode);
        }

        [Test]
        public void AWellFormedWorldReportsNoErrors()
        {
            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);
            Assert.AreEqual(0, report.ErrorCount, report.ToMarkdown());
        }

        [Test]
        public void ReportsMultipleSceneDescriptors()
        {
            var nested = new GameObject("NestedDescriptor");
            nested.transform.SetParent(_world.transform);
            nested.AddComponent<VRCSceneDescriptor>();

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(Mentions(report, "2 VRCSceneDescriptor"));
        }

        [Test]
        public void ReportsAWorldWithNoSpawnPoints()
        {
            _world.GetComponent<VRCSceneDescriptor>().spawns = new Transform[0];

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(Mentions(report, "spawn point"));
        }

        [Test]
        public void ReportsAnEmptySpawnSlot()
        {
            // With Random spawn order an empty slot sends the player to the
            // world origin, so this is an error rather than untidiness.
            _world.GetComponent<VRCSceneDescriptor>().spawns =
                new[] { _spawn.transform, null };

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(Mentions(report, "empty"));
        }

        [Test]
        public void ReportsATiltedSpawnPoint()
        {
            _spawn.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsTrue(Mentions(report, "tilted"));
        }

        [Test]
        public void AcceptsASpawnPointRotatedAroundYaw()
        {
            _spawn.transform.rotation = Quaternion.Euler(0f, 145f, 0f);

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsFalse(Mentions(report, "tilted"), "only the yaw is used, so yaw is fine");
        }

        [Test]
        public void ReportsARespawnPlaneAboveTheFloor()
        {
            // The floor sits at y = 0, so a respawn plane at y = 5 catches
            // anyone standing on it.
            _world.GetComponent<VRCSceneDescriptor>().RespawnHeightY = 5f;

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(Mentions(report, "RespawnHeightY"));
        }

        [Test]
        public void IgnoresDisabledRenderersWhenFindingTheLowestGeometry()
        {
            GameObject disabled = GameObject.CreatePrimitive(PrimitiveType.Cube);
            disabled.name = "DisabledBasement";
            disabled.transform.SetParent(_world.transform);
            disabled.transform.position = new Vector3(0f, -100f, 0f);
            disabled.GetComponent<Renderer>().enabled = false;
            _world.GetComponent<VRCSceneDescriptor>().RespawnHeightY = -50f;

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsFalse(Mentions(report, "RespawnHeightY"),
                "disabled geometry must not move the respawn boundary");
        }

        [Test]
        public void SkipsTheRespawnGeometryCheckWhenTheWorldHasNoRenderer()
        {
            Object.DestroyImmediate(_floor);
            _floor = null;
            _world.GetComponent<VRCSceneDescriptor>().RespawnHeightY = 5f;

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsFalse(Mentions(report, "RespawnHeightY"));
        }

        [Test]
        public void ReportsTheDefaultReferenceCameraBehaviour()
        {
            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsTrue(Mentions(report, "No Reference Camera"));
        }

        [Test]
        public void ReportsAReferenceCameraWithNoCameraComponent()
        {
            var broken = new GameObject("NotACamera");
            broken.transform.SetParent(_world.transform);
            _world.GetComponent<VRCSceneDescriptor>().ReferenceCamera = broken;

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(Mentions(report, "no Camera component"));
        }

        [Test]
        public void ReportsAReferenceCameraWithAnExcessiveNearClipPlane()
        {
            var reference = new GameObject("ReferenceCamera");
            reference.transform.SetParent(_world.transform);
            reference.AddComponent<Camera>().nearClipPlane = 0.1f;
            _world.GetComponent<VRCSceneDescriptor>().ReferenceCamera = reference;

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsTrue(Mentions(report, "near clip plane"));
            Assert.IsTrue(HasRow(report, "World", "Reference Camera", "ReferenceCamera"));
        }

        [Test]
        public void ReportsActiveMirrorsAndTheAdvisoryThreshold()
        {
            for (int i = 0; i < 3; i++)
            {
                var mirror = new GameObject("Mirror" + i);
                mirror.transform.SetParent(_world.transform);
                mirror.AddComponent<VRCMirrorReflection>();
            }

            InspectionReport report = InspectApi.Inspect(_world, InspectMode.World);

            Assert.IsTrue(HasRow(report, "World", "Mirrors (active / total)", "3 / 3"));
            Assert.IsTrue(Mentions(report, "enabled when the scene loads"));
            Assert.IsTrue(Mentions(report, "own advisory threshold"));
        }

        [Test]
        public void ScanningLeavesTheWorldUntouched()
        {
            var descriptor = _world.GetComponent<VRCSceneDescriptor>();
            string before = JsonUtility.ToJson(descriptor);

            InspectApi.Inspect(_world, InspectMode.World);

            Assert.AreEqual(before, JsonUtility.ToJson(descriptor),
                "the module must not write to the descriptor");
        }

        private static bool Mentions(InspectionReport report, string fragment)
        {
            foreach (InspectionItem item in report.Items)
            {
                if (item.Message.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRow(
            InspectionReport report, string group, string label, string value)
        {
            foreach (StatRow row in report.Rows)
            {
                if (row.Group == group && row.Label == label && row.Value == value)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
