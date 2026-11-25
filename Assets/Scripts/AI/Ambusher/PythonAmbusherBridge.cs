using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Optional development bridge for demonstrating that decisions originate in the Python engine.
/// Keep disabled in production builds unless Python is deployed beside the game.
/// </summary>
[RequireComponent(typeof(EnemyActor))]
public sealed class PythonAmbusherBridge : MonoBehaviour
{
    [SerializeField] private bool launchPython;
    [SerializeField] private string pythonExecutable = "python";
    [SerializeField, Min(.05f)] private float decisionInterval = .2f;
    private EnemyActor actor;
    private Process process;
    private float nextDecision;

    [Serializable]
    private sealed class Request
    {
        public float now;
        public float[] agent;
        public float[] player;
        public float[] forward;
        public float[] velocity;
        public bool visible;
        public bool reloading;
        public bool route_failed;
        public bool attack_ready;
    }

    [Serializable]
    private sealed class Response
    {
        public string command;
        public string state;
        public float[] target;
        public string reason;
        public string error;
    }

    private void Awake() => actor = GetComponent<EnemyActor>();

    private void OnEnable()
    {
        if (!launchPython) return;
        string root = Directory.GetParent(Application.dataPath).FullName;
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = "-m PythonAI.ambusher.service",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
    }

    private void Update()
    {
        if (process == null || process.HasExited || actor == null || !actor.CanThink || Time.time < nextDecision) return;
        nextDecision = Time.time + decisionInterval;
        var senses = actor.Perception;
        var request = new Request
        {
            now = Time.time,
            agent = Pair(transform.position),
            player = senses.HasMemory ? Pair(senses.LastKnownPosition) : null,
            forward = Pair(senses.ObservedForward),
            velocity = Pair(senses.ObservedVelocity),
            visible = senses.Visible,
            reloading = senses.PlayerReloading,
            route_failed = actor.Movement.Failed,
            attack_ready = actor.Combat.IsReady
        };
        process.StandardInput.WriteLine(JsonUtility.ToJson(request));
        Response response = JsonUtility.FromJson<Response>(process.StandardOutput.ReadLine());
        if (response == null || !string.IsNullOrEmpty(response.error)) return;
        Apply(response);
    }

    private void Apply(Response response)
    {
        actor.Decision = "Python Ambusher / " + response.state + " / " + response.reason;
        Vector3 target = response.target != null && response.target.Length == 2
            ? new Vector3(response.target[0], transform.position.y, response.target[1])
            : transform.position;
        switch (response.command)
        {
            case "move":
            case "investigate": actor.Movement.MoveTo(target, 3.1f, .6f); break;
            case "face":
            case "warn": actor.Movement.Stop(); actor.Movement.Face(target); break;
            case "attack": actor.Combat.TryAttack(); break;
            default: actor.Movement.Stop(); break;
        }
    }

    private static float[] Pair(Vector3 value) => new[] { value.x, value.z };

    private void OnDisable()
    {
        if (process == null) return;
        try
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(250)) process.Kill();
        }
        catch (InvalidOperationException) { }
        process.Dispose();
        process = null;
    }
}

