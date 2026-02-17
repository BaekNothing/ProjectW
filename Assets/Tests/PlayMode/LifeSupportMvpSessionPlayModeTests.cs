using System.Collections;
using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectW.Tests.PlayMode
{
    public class LifeSupportMvpSessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SessionComponent_AdvancesAndLogs_InPlayMode()
        {
            var go = new GameObject("LifeSupportMvpSession_PlayModeTest");
            var session = go.AddComponent<LifeSupportMvpSession>();

            LogAssert.Expect(LogType.Log, "[MVP] Session started manually.");
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[MVP\] Tick 1:"));

            var step = session.RunSingleTick();

            Assert.IsNotNull(step);
            Assert.Greater(step.logs.Count, 0);

            Object.Destroy(go);
            yield return null;
        }
    }
}
