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
            Debug.Log("HeadlessAutoChessDemoSceneRunner: scenario runtime removed pending destructive refactor — see AutoChessDemo事实.md");
        }
    }
}
