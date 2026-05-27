using System;
using System.Collections;
using UnityEngine;

namespace GAS.Runtime
{
    public sealed class HeadlessAutoChessDemoSceneRunner : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        public bool HasResult { get; private set; }

        private IEnumerator Start()
        {
            if (!runOnStart)
                yield break;

            yield return null;
            var request = HeadlessAutoChessRuntimeSystemBootstrap.RequestDefaultScenario();
            HasResult = request != Unity.Entities.Entity.Null;
            Debug.Log(
                "HeadlessAutoChessDemoSceneRunner: queued generated AutoChess scenario bootstrap "
                + $"request={request.Index}, scenario={HeadlessAutoChessScenario.ScenarioDefaultDuel}");
        }
    }
}
