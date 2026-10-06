// Author: Omid Ameri
// Course: Multiplayer and Networking in Games and XR

using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class ExtractionManager : NetworkBehaviour
{
    private enum ExtractionState
    {
        Idle,
        EnRouteTimerRunning,
        HelicopterArrived,
        ExtractionCountdownRunning,
        Completed
    }

    [Header("Timing")]
    [SerializeField] private float helicopterEnRouteSeconds = 70f;   // ✅ 70 seconds
    [SerializeField] private float extractionCountdownSeconds = 30f;

    [Header("Audio (2D)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip helicopterOnTheWayClip;
    [SerializeField] private AudioClip helicopterArrivedClip;
    [SerializeField] private AudioClip extractionSuccessClip;

    [Header("Helicopter")]
    [SerializeField] private GameObject helicopterRoot; // visuals only
    [SerializeField] private Animator helicopterAnimator;
    [SerializeField] private string takeOffTriggerName = "TakeOff";

    [Header("UI")]
    [SerializeField] private TMP_Text extractionCountdownText;

    [Header("Zombie Spawner")]
    [SerializeField] private ZombieSpawner zombieSpawner;
    [Tooltip("Index of your 'Final Stage' inside ZombieSpawner.stages list.")]
    [SerializeField] private int finalStageIndex = 2;

    private ExtractionState _state = ExtractionState.Idle;

    private readonly HashSet<ulong> _arrivedPlayers = new HashSet<ulong>();
    private ulong _firstArriverClientId;

    private Coroutine _enRouteRoutine;
    private Coroutine _extractionRoutine;

    private readonly NetworkVariable<float> _extractionRemaining =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (extractionCountdownText != null)
            extractionCountdownText.gameObject.SetActive(false);

        // Local safety; server enforces too
        if (helicopterRoot != null)
            helicopterRoot.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Ensure initial helicopter hidden state for everyone (including late joiners)
            SetHelicopterVisibleClientRpc(false);
        }

        if (IsClient)
        {
            _extractionRemaining.OnValueChanged += OnExtractionRemainingChanged;
            UpdateCountdownUI(_extractionRemaining.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
            _extractionRemaining.OnValueChanged -= OnExtractionRemainingChanged;
    }

    // Called by SafeRoomExitTrigger (server side)
    public void NotifySafeRoomExited(ulong clientId)
    {
        if (!IsServer) return;
        if (_state != ExtractionState.Idle) return;

        _state = ExtractionState.EnRouteTimerRunning;

        SetHelicopterVisibleClientRpc(false);
        PlayClipClientRpc(AudioClipId.HelicopterOnTheWay);

        _enRouteRoutine = StartCoroutine(HelicopterEnRouteRoutine());
    }

    // Called by HelicopterZoneTrigger (server side)
    public void NotifyPlayerEnteredHelicopterZone(ulong clientId)
    {
        if (!IsServer) return;

        if (_state != ExtractionState.HelicopterArrived &&
            _state != ExtractionState.ExtractionCountdownRunning)
            return;

        if (_arrivedPlayers.Add(clientId))
        {
            // First arrival starts the visible countdown
            if (_state == ExtractionState.HelicopterArrived)
            {
                _firstArriverClientId = clientId;
                _state = ExtractionState.ExtractionCountdownRunning;
                _extractionRoutine = StartCoroutine(ExtractionCountdownRoutine());
            }

            // If at least 2 arrived, complete immediately
            if (_state == ExtractionState.ExtractionCountdownRunning && _arrivedPlayers.Count >= 2)
            {
                CompleteExtraction();
            }
        }
    }

    private IEnumerator HelicopterEnRouteRoutine()
    {
        float t = 0f;
        while (t < helicopterEnRouteSeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }

        _state = ExtractionState.HelicopterArrived;

        // Switch zombies to Final Stage
        if (zombieSpawner != null)
            zombieSpawner.ForceStage(finalStageIndex);

        // Show helicopter visuals for everyone
        SetHelicopterVisibleClientRpc(true);

        // Everyone hears arrival instruction
        PlayClipClientRpc(AudioClipId.HelicopterArrived);
    }

    private IEnumerator ExtractionCountdownRoutine()
    {
        _extractionRemaining.Value = extractionCountdownSeconds;

        while (_extractionRemaining.Value > 0f)
        {
            _extractionRemaining.Value = Mathf.Max(0f, _extractionRemaining.Value - Time.deltaTime);

            if (_arrivedPlayers.Count >= 2)
            {
                CompleteExtraction();
                yield break;
            }

            yield return null;
        }

        // Timeout: extraction still happens with whoever arrived
        CompleteExtraction();
    }

    private void CompleteExtraction()
    {
        if (!IsServer) return;
        if (_state == ExtractionState.Completed) return;

        _state = ExtractionState.Completed;

        if (_enRouteRoutine != null) StopCoroutine(_enRouteRoutine);
        if (_extractionRoutine != null) StopCoroutine(_extractionRoutine);

        _extractionRemaining.Value = 0f;

        // Play success audio + takeoff animation for all
        PlayClipClientRpc(AudioClipId.ExtractionSuccess);
        TriggerHelicopterTakeOffClientRpc();

        // Decide winners:
        // - If 2+ arrived: winners are the players who arrived
        // - Else: only first arriver wins
        HashSet<ulong> winners = new HashSet<ulong>();
        if (_arrivedPlayers.Count >= 2)
        {
            foreach (var id in _arrivedPlayers)
                winners.Add(id);
        }
        else
        {
            winners.Add(_firstArriverClientId);
        }

        // ✅ Make winners invulnerable on the server so they can NEVER die after victory.
        MakeWinnersInvulnerable(winners);

        // Show victory UI ONLY to winners
        SendVictoryToWinners(winners);

        // Optional visuals: hide/disable winners so it looks like they boarded
        DisableWinningPlayersVisuals(winners);
    }

    // ---------------------------------------------------------
    // ✅ Server-side invulnerability for winners
    // ---------------------------------------------------------

    private void MakeWinnersInvulnerable(HashSet<ulong> winners)
    {
        if (!IsServer) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            ulong cid = client.ClientId;
            if (!winners.Contains(cid)) continue;

            var health = client.PlayerObject.GetComponent<Health>();
            if (health != null)
                health.SetInvulnerable(true);
        }
    }

    // ---------------------------------------------------------
    // VICTORY UI (winners only)
    // ---------------------------------------------------------

    private void SendVictoryToWinners(HashSet<ulong> winners)
    {
        if (!IsServer) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong cid = client.ClientId;
            bool isWinner = winners.Contains(cid);

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { cid }
                }
            };

            ShowVictoryTargetedClientRpc(isWinner, rpcParams);
        }
    }

    [ClientRpc]
    private void ShowVictoryTargetedClientRpc(bool isWinner, ClientRpcParams rpcParams = default)
    {
        if (!isWinner) return;

        var ui = Object.FindFirstObjectByType<PlayerEndScreenUI>();
        if (ui != null)
            ui.ShowVictory("VICTORY!\nExtraction successful!");
    }

    // ---------------------------------------------------------
    // Optional: hide/disable WINNERS visually (boarding effect)
    // ---------------------------------------------------------

    private void DisableWinningPlayersVisuals(HashSet<ulong> winners)
    {
        if (!IsServer) return;

        ulong[] winnerIds = new ulong[winners.Count];
        int i = 0;
        foreach (var id in winners)
            winnerIds[i++] = id;

        DisableWinningPlayersVisualsClientRpc(winnerIds);
    }

    [ClientRpc]
    private void DisableWinningPlayersVisualsClientRpc(ulong[] winnerClientIds)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        for (int i = 0; i < players.Length; i++)
        {
            GameObject p = players[i];
            if (p == null) continue;

            if (!p.TryGetComponent<NetworkObject>(out var nobj))
                continue;

            bool isWinner = false;
            for (int w = 0; w < winnerClientIds.Length; w++)
            {
                if (nobj.OwnerClientId == winnerClientIds[w])
                {
                    isWinner = true;
                    break;
                }
            }

            if (!isWinner)
                continue;

            // Stop movement (optional)
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var controller = p.GetComponent<MultiplayerPlayerController>();
            if (controller != null) controller.enabled = false;

            // Hide visuals (boarding look)
            var renderers = p.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].enabled = false;
        }
    }

    // ---------------------------------------------------------
    // UI (extraction countdown)
    // ---------------------------------------------------------

    private void OnExtractionRemainingChanged(float previous, float current)
    {
        UpdateCountdownUI(current);
    }

    private void UpdateCountdownUI(float secondsRemaining)
    {
        if (extractionCountdownText == null) return;

        bool show = secondsRemaining > 0.01f;
        extractionCountdownText.gameObject.SetActive(show);

        if (show)
            extractionCountdownText.text = $"EXTRACTION IN: {Mathf.CeilToInt(secondsRemaining)}";
    }

    // ---------------------------------------------------------
    // AUDIO + HELI VISIBILITY + ANIMATION
    // ---------------------------------------------------------

    private enum AudioClipId : byte
    {
        HelicopterOnTheWay = 0,
        HelicopterArrived = 1,
        ExtractionSuccess = 2
    }

    [ClientRpc]
    private void PlayClipClientRpc(AudioClipId id)
    {
        if (audioSource == null) return;

        AudioClip clip = id switch
        {
            AudioClipId.HelicopterOnTheWay => helicopterOnTheWayClip,
            AudioClipId.HelicopterArrived => helicopterArrivedClip,
            AudioClipId.ExtractionSuccess => extractionSuccessClip,
            _ => null
        };

        if (clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    [ClientRpc]
    private void SetHelicopterVisibleClientRpc(bool visible)
    {
        if (helicopterRoot == null) return;
        helicopterRoot.SetActive(visible);
    }

    [ClientRpc]
    private void TriggerHelicopterTakeOffClientRpc()
    {
        if (helicopterAnimator == null) return;
        if (string.IsNullOrWhiteSpace(takeOffTriggerName)) return;

        helicopterAnimator.SetTrigger(takeOffTriggerName);
    }
}