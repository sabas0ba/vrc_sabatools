// EditMode tests for the avatar module, run against the real Avatars SDK.
//
// The offline harness in .github/verify/offline covers AvatarLimits, which is
// arithmetic. What it cannot reach is everything this file exercises: that the
// SDK field names the module reads still exist, that TypeCache finds the
// module from the core assembly without a reference, and that Auto mode
// resolves to Avatar because a real descriptor was seen.
using NUnit.Framework;
using SabaTools.Inspect;
using SabaTools.Inspect.Editors;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace SabaTools.Inspect.Avatar.SdkTests
{
    public class AvatarModuleTests
    {
        private GameObject _avatar;

        [SetUp]
        public void CreateAvatar()
        {
            _avatar = new GameObject("TestAvatar");
            _avatar.AddComponent<VRCAvatarDescriptor>().ViewPosition = new Vector3(0f, 1.6f, 0.1f);
        }

        [TearDown]
        public void DestroyAvatar()
        {
            if (_avatar != null)
            {
                Object.DestroyImmediate(_avatar);
            }
        }

        [Test]
        public void AutoModeResolvesToAvatarFromARealDescriptor()
        {
            // The module claimed it, not the core type-name census: that is
            // what makes the rest of these checks meaningful.
            InspectionReport report = InspectApi.Inspect(_avatar);
            Assert.AreEqual(InspectMode.Avatar, report.Mode);
        }

        [Test]
        public void TheDescriptorDiagnosisIsNotDuplicated()
        {
            // Core suppresses its own type-name descriptor warning when a
            // module is running. With a descriptor present, neither should say
            // anything about one being missing.
            var bare = new GameObject("NoDescriptor");
            try
            {
                InspectionReport report = InspectApi.Inspect(bare, InspectMode.Avatar);
                int mentions = 0;
                foreach (InspectionItem item in report.Items)
                {
                    if (item.Message.Contains("VRCAvatarDescriptor"))
                    {
                        mentions++;
                    }
                }
                Assert.AreEqual(1, mentions,
                    "exactly one component should report the missing descriptor");
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void ReportsAViewPositionLeftAtTheOrigin()
        {
            _avatar.GetComponent<VRCAvatarDescriptor>().ViewPosition = Vector3.zero;

            InspectionReport report = InspectApi.Inspect(_avatar, InspectMode.Avatar);

            Assert.Greater(report.ErrorCount, 0);
            Assert.IsTrue(Mentions(report, "ViewPosition"));
        }

        [Test]
        public void AcceptsAReasonableViewPosition()
        {
            InspectionReport report = InspectApi.Inspect(_avatar, InspectMode.Avatar);
            Assert.IsFalse(Mentions(report, "ViewPosition"),
                "a viewpoint at eye height should not be reported");
        }

        [Test]
        public void ReportsExpressionParametersOverBudget()
        {
            VRCExpressionParameters parameters =
                ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var entries = new VRCExpressionParameters.Parameter[40];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new VRCExpressionParameters.Parameter
                {
                    name = "Param" + i,
                    valueType = VRCExpressionParameters.ValueType.Int,
                };
            }
            parameters.parameters = entries;
            _avatar.GetComponent<VRCAvatarDescriptor>().expressionParameters = parameters;

            try
            {
                // 40 Ints is 320 bits against a 256 bit budget.
                InspectionReport report = InspectApi.Inspect(_avatar, InspectMode.Avatar);
                Assert.Greater(report.ErrorCount, 0);
                Assert.IsTrue(Mentions(report, "bit"));
            }
            finally
            {
                Object.DestroyImmediate(parameters);
            }
        }

        [Test]
        public void ReportsDuplicateExpressionParameterNames()
        {
            VRCExpressionParameters parameters =
                ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "Same", valueType = VRCExpressionParameters.ValueType.Bool,
                },
                new VRCExpressionParameters.Parameter
                {
                    name = "Same", valueType = VRCExpressionParameters.ValueType.Bool,
                },
            };
            _avatar.GetComponent<VRCAvatarDescriptor>().expressionParameters = parameters;

            try
            {
                InspectionReport report = InspectApi.Inspect(_avatar, InspectMode.Avatar);
                Assert.IsTrue(Mentions(report, "more than"),
                    "a duplicated parameter name should be reported");
            }
            finally
            {
                Object.DestroyImmediate(parameters);
            }
        }

        [Test]
        public void ReportsAMenuOverTheControlLimit()
        {
            VRCExpressionsMenu menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            for (int i = 0; i <= AvatarLimits.ControlsPerMenu; i++)
            {
                menu.controls.Add(new VRCExpressionsMenu.Control { name = "Control" + i });
            }
            _avatar.GetComponent<VRCAvatarDescriptor>().expressionsMenu = menu;

            try
            {
                InspectionReport report = InspectApi.Inspect(_avatar, InspectMode.Avatar);
                Assert.Greater(report.ErrorCount, 0);
                Assert.IsTrue(Mentions(report, "controls"));
            }
            finally
            {
                Object.DestroyImmediate(menu);
            }
        }

        [Test]
        public void SurvivesAMenuThatPointsAtItself()
        {
            // The SDK lets this be authored, and a walk without cycle
            // detection would hang the editor rather than report anything.
            VRCExpressionsMenu menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Loop",
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = menu,
            });
            _avatar.GetComponent<VRCAvatarDescriptor>().expressionsMenu = menu;

            try
            {
                InspectionReport report = InspectApi.Inspect(_avatar, InspectMode.Avatar);
                Assert.IsTrue(Mentions(report, "reachable from itself"));
            }
            finally
            {
                Object.DestroyImmediate(menu);
            }
        }

        [Test]
        public void ScanningLeavesTheAvatarUntouched()
        {
            var descriptor = _avatar.GetComponent<VRCAvatarDescriptor>();
            string before = JsonUtility.ToJson(descriptor);

            InspectApi.Inspect(_avatar, InspectMode.Avatar);

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
    }
}
