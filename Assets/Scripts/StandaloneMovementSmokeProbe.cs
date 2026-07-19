#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StandaloneMovementSmokeProbe
{
    private const string LogPrefix = "[StandaloneMovementSmoke]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Debug.Log($"{LogPrefix} Runtime probe initialized.");
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, "MainMenu", StringComparison.Ordinal) &&
            !string.Equals(scene.name, "Gameplay", StringComparison.Ordinal))
        {
            return;
        }

        GameObject runnerObject = new GameObject($"StandaloneMovementSmoke_{scene.name}");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<Runner>();
    }

    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            if (string.Equals(sceneName, "MainMenu", StringComparison.Ordinal))
            {
                yield return null;
                MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();

                if (menu == null)
                {
                    Fail("MainMenuController was not found.");
                    yield break;
                }

                Debug.Log($"{LogPrefix} Loading Gameplay through MainMenuController.NewGame().");
                menu.NewGame();
                Destroy(gameObject);
                yield break;
            }

            float readinessDeadline = Time.realtimeSinceStartup + 15f;
            PointClickPlayerMovement movement = null;

            while (Time.realtimeSinceStartup < readinessDeadline)
            {
                movement = PointClickPlayerMovement.FindActiveRoutePlanner();

                if (movement != null &&
                    movement.TryGetWorldPointFromLogicalPosition(movement.LogicalPosition, out _))
                {
                    break;
                }

                yield return null;
            }

            if (movement == null ||
                !movement.TryGetWorldPointFromLogicalPosition(movement.LogicalPosition, out Vector2 startWorld))
            {
                Fail("PointClickPlayerMovement did not become ready.");
                yield break;
            }

            Vector2 startLogical = movement.LogicalPosition;
            SpriteRenderer[] renderers = movement.GetComponentsInChildren<SpriteRenderer>(true);
            string rendererSummary = string.Empty;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                rendererSummary +=
                    $" renderer[{i}]={renderer.name} sprite={renderer.sprite?.name ?? "null"} " +
                    $"boundsMin={renderer.bounds.min} boundsMax={renderer.bounds.max} " +
                    $"lossyScale={renderer.transform.lossyScale};";
            }

            Debug.Log(
                $"{LogPrefix} Ready logical={startLogical} world={startWorld} " +
                $"root={movement.transform.position}.{rendererSummary}");
            Vector2[] screenPoints =
            {
                new Vector2(Screen.width * 0.65f, Screen.height * 0.28f),
                new Vector2(Screen.width * 0.35f, Screen.height * 0.28f),
                new Vector2(Screen.width * 0.55f, Screen.height * 0.20f),
                new Vector2(Screen.width * 0.45f, Screen.height * 0.38f)
            };
            bool destinationAccepted = false;
            PointClickPlayerMovement.MovementTargetQuery acceptedQuery = default;

            for (int i = 0; i < screenPoints.Length; i++)
            {
                if (!movement.TryEvaluateMovementAtScreenPoint(screenPoints[i], true, out PointClickPlayerMovement.MovementTargetQuery query))
                {
                    continue;
                }

                Debug.Log(
                    $"{LogPrefix} Candidate screen={screenPoints[i]} " +
                    $"requested={query.RequestedLogicalPosition} destination={query.Destination} " +
                    $"reachable={query.HasReachableDestination}.");

                if (query.HasReachableDestination &&
                    movement.TrySetDestinationFromScreenPoint(screenPoints[i], true, true))
                {
                    destinationAccepted = true;
                    acceptedQuery = query;
                    break;
                }
            }

            if (!destinationAccepted)
            {
                Fail($"No nearby destination was accepted from logical {startLogical}.");
                yield break;
            }

            if (Mathf.Abs(acceptedQuery.RequestedLogicalPosition.x) > 20f ||
                Mathf.Abs(acceptedQuery.RequestedLogicalPosition.y) > 20f)
            {
                Fail($"Screen conversion still uses canvas pixels: requested={acceptedQuery.RequestedLogicalPosition}.");
                yield break;
            }

            float movementDeadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < movementDeadline)
            {
                yield return null;
            }

            movement.TryGetWorldPointFromLogicalPosition(movement.LogicalPosition, out Vector2 endWorld);
            float logicalDistance = Vector2.Distance(startLogical, movement.LogicalPosition);
            float worldDistance = Vector2.Distance(startWorld, endWorld);

            if (logicalDistance < 0.25f || worldDistance < 0.25f)
            {
                Fail(
                    $"Movement was too small: logicalDistance={logicalDistance:0.###}, " +
                    $"worldDistance={worldDistance:0.###}, startLogical={startLogical}, " +
                    $"endLogical={movement.LogicalPosition}.");
                yield break;
            }

            Debug.Log(
                $"{LogPrefix} PASS logicalDistance={logicalDistance:0.###} " +
                $"worldDistance={worldDistance:0.###} startLogical={startLogical} " +
                $"endLogical={movement.LogicalPosition}.");
            Application.Quit(0);
        }

        private static void Fail(string message)
        {
            Debug.LogError($"{LogPrefix} FAIL {message}");
            Application.Quit(3);
        }
    }
}
#endif
