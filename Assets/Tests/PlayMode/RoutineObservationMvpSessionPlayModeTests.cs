using System.Collections;
using NUnit.Framework;
using ProjectW.IngameMvp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectW.Tests.PlayMode
{
    public class RoutineObservationMvpSessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_HasRoutineSessionAndTimeHud()
        {
            var load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            Assert.IsNotNull(load);
            while (!load.isDone)
            {
                yield return null;
            }

            var session = Object.FindFirstObjectByType<RoutineObservationMvpSession>();
            Assert.IsNotNull(session);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[RoutineMVP\] Day 1 \|"));
            session.AdvanceOneTick();
            yield return null;
        }
    }
}
