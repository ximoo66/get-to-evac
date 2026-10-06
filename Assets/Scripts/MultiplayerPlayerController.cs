// Author: Omid Ameri
// Course: Multiplayer and Networking in Games and XR

using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public sealed class MultiplayerPlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader myReader;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.8f;

    [Header("Acceleration Settings")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;

    [Header("Multipliers")]
    [SerializeField] private float runMultiplier = 2f;

    // Runtime
    private CharacterController controller;
    private Vector2 moveInput;
    private Transform camTransform;
    private Camera mainCam;

    // Prediction/runtime
    private Vector3 velocity;
    private float baseSpeed;
    private float forwardSpeed;
    private float desiredForwardSpeed;

    // Local input state (owner only)
    private bool isRunning;
    private bool isCharging;
    private bool jumpRequestedThisFrame;
    private bool localAttackHeld;
    private bool localIsJumping;

    // Server-side cached input
    private Vector3 serverDesiredMove;
    private Vector3 serverDesiredLook;
    private bool serverIsRunning;
    private bool serverIsCharging;
    private float serverForwardSpeed;

    // Networked animation state (server writable)
    private readonly NetworkVariable<float> currentRunningSpeedNV =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> networkIsRunningNV =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> networkIsChargingNV =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> networkIsJumpingNV =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> networkIsShootingNV =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #region Scene init (server sets spawn, owner binds camera)
    [ClientRpc]
    private void UpdatePlayerAfterLoadClientRpc()
    {
        // Server chooses spawn position once (authoritative)
        if (IsServer)
            StartCoroutine(ServerSpawnWhenTerrainReady());

        if (!IsOwner) return;

        var cineCam = Object.FindFirstObjectByType<CinemachineCamera>();
        Transform cameraTarget = transform.Find("CameraTarget") ?? transform;

        if (cineCam != null)
        {
            cineCam.Follow = cameraTarget;
            cineCam.LookAt = cameraTarget;
        }
        else if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
            camTransform.position = cameraTarget.position + new Vector3(0f, 2f, -4f);
            camTransform.LookAt(cameraTarget);
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private IEnumerator ServerSpawnWhenTerrainReady()
    {
        while (Terrain.activeTerrain == null)
            yield return null;

        yield return null;
        yield return null;

        float posx = 6f + Random.Range(-1f, 1f);
        float posz = -32f + Random.Range(-1f, 1f);
        float posy = Terrain.activeTerrain.SampleHeight(new Vector3(posx, 0f, posz));

        Vector3 spawnPos = new Vector3(posx, posy + 3f, posz);

        // Why: only the server sets spawn so everyone receives the same position via NetworkTransform.
        transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
    }

    private void OnLoadCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        UpdatePlayerAfterLoadClientRpc();
    }
    #endregion

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        baseSpeed = speed;
        controller = GetComponent<CharacterController>();

        if (IsOwner && myReader != null)
        {
            myReader.MoveEvent += MoveEvent;
            myReader.CancelEvent += CancelEvent;
            myReader.RunEvent += RunEvent;
            myReader.JumpEvent += JumpEvent;
            myReader.PrimaryAttackEvent += PrimaryAttackEvent;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadCompleted;

        if (IsOwner && Camera.main != null)
        {
            mainCam = Camera.main;
            camTransform = mainCam.transform;
        }

        if (IsServer)
        {
            try
            {
                UserData userData = HostSingleton.Instance.GameManager.NetworkServer.GetUserDataByClientId(OwnerClientId);
                PlayerName.Value = userData != null ? userData.userName : "Unknown";
            }
            catch
            {
                PlayerName.Value = "Unknown";
            }

            UpdatePlayerAfterLoadClientRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && myReader != null)
        {
            myReader.MoveEvent -= MoveEvent;
            myReader.CancelEvent -= CancelEvent;
            myReader.RunEvent -= RunEvent;
            myReader.JumpEvent -= JumpEvent;
            myReader.PrimaryAttackEvent -= PrimaryAttackEvent;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadCompleted;

        base.OnNetworkDespawn();
    }

    #region Input
    private void MoveEvent(Vector2 direction) => moveInput = direction;

    private void CancelEvent(bool cancel)
    {
        Cursor.lockState = (Cursor.lockState == CursorLockMode.Confined) ? CursorLockMode.None : CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private void PrimaryAttackEvent(bool shoot)
    {
        localAttackHeld = shoot;

        if (shoot)
        {
            isRunning = false;
            desiredForwardSpeed = Mathf.Min(desiredForwardSpeed, baseSpeed);
        }
    }

    private void RunEvent(bool run)
    {
        if (localAttackHeld)
        {
            isRunning = false;
            return;
        }

        isRunning = run;
    }

    private void JumpEvent(bool jump)
    {
        if (jump)
            jumpRequestedThisFrame = true;
    }
    #endregion

    private void Update()
    {
        // Non-owners should ALWAYS animate (never gate on "init")
        if (!IsOwner)
        {
            UpdateAnimationsFromNetwork();
            return;
        }

        // Owner: compute input & send to server
        if (mainCam == null && Camera.main != null)
        {
            mainCam = Camera.main;
            camTransform = mainCam.transform;
        }

        Vector3 camForward = (camTransform != null)
            ? Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized
            : Vector3.forward;

        Vector3 camRight = (camTransform != null) ? camTransform.right : Vector3.right;
        Vector3 desiredMove = camRight * moveInput.x + camForward * moveInput.y;

        float targetSpeed = CalculateLocalTargetSpeed(desiredMove);
        float accel = (targetSpeed > forwardSpeed) ? acceleration : deceleration;
        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, accel * Time.deltaTime);

        Vector3 worldMoveDir = desiredMove.sqrMagnitude > 0.0001f ? desiredMove.normalized : Vector3.zero;
        Vector3 lookDir = ComputeLookDirection();

        bool requestJump = jumpRequestedThisFrame;
        jumpRequestedThisFrame = false;

        // ✅ Client-side prediction ONLY for real clients (NOT host).
        // Why: host already sees server movement instantly; prediction there causes mismatches.
        bool shouldPredictLocally = !IsServer;

        if (shouldPredictLocally)
            PredictMoveLocally(worldMoveDir, lookDir, requestJump);

        // Send input to server (server is authoritative)
        SendInputServerRpc(worldMoveDir, isRunning, isCharging, forwardSpeed, requestJump, localAttackHeld, lookDir);

        // Owner anim stays responsive
        UpdateAnimationsLocal();
    }

    private void PredictMoveLocally(Vector3 moveDir, Vector3 lookDir, bool requestJump)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        float effectiveSpeed = CalculatePredictedSpeed();
        float forward = (moveDir.sqrMagnitude > 0.01f) ? effectiveSpeed : 0f;
        Vector3 move = moveDir * forward;

        if (controller != null)
            controller.Move(move * Time.deltaTime);
        else
            transform.position += move * Time.deltaTime;

        if (lookDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        if (requestJump && controller != null && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            localIsJumping = true;
        }

        velocity.y += gravity * Time.deltaTime;

        if (controller != null)
        {
            controller.Move(velocity * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;
                localIsJumping = false;
            }
        }
    }

    private float CalculatePredictedSpeed()
    {
        if (isCharging) return 0f;

        bool runningAllowed = isRunning && !localAttackHeld;
        return runningAllowed ? baseSpeed * runMultiplier : baseSpeed;
    }

    private float CalculateLocalTargetSpeed(Vector3 desiredMove)
    {
        if (isCharging) return 0f;

        float effective = (isRunning && !localAttackHeld) ? baseSpeed * runMultiplier : baseSpeed;
        desiredForwardSpeed = desiredMove.sqrMagnitude > 0.01f ? effective : 0f;
        return desiredForwardSpeed;
    }

    private Vector3 ComputeLookDirection()
    {
        if (mainCam == null) return Vector3.zero;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return Vector3.zero;

        Vector3 lookPos = hit.point;
        lookPos.y = transform.position.y;

        Vector3 dir = lookPos - transform.position;
        if (dir.sqrMagnitude < 0.01f)
            return Vector3.zero;

        return dir.normalized;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendInputServerRpc(
        Vector3 worldMoveDir,
        bool running,
        bool charging,
        float clientForwardSpeed,
        bool requestJump,
        bool requestShooting,
        Vector3 worldLookDir,
        ServerRpcParams rpcParams = default)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        serverDesiredMove = new Vector3(worldMoveDir.x, 0f, worldMoveDir.z).sqrMagnitude > 0.0001f
            ? new Vector3(worldMoveDir.x, 0f, worldMoveDir.z).normalized
            : Vector3.zero;

        serverDesiredLook = new Vector3(worldLookDir.x, 0f, worldLookDir.z).sqrMagnitude > 0.0001f
            ? new Vector3(worldLookDir.x, 0f, worldLookDir.z).normalized
            : Vector3.zero;

        serverIsRunning = requestShooting ? false : running;
        serverIsCharging = charging;
        serverForwardSpeed = clientForwardSpeed;

        // Networked animator state
        networkIsShootingNV.Value = requestShooting;
        networkIsRunningNV.Value = running;
        networkIsChargingNV.Value = charging;
        currentRunningSpeedNV.Value = clientForwardSpeed;

        if (requestJump && controller != null && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            networkIsJumpingNV.Value = true;
        }

        float effectiveSpeed = CalculateSpeedOnServer(serverIsRunning, serverIsCharging);
        float forward = (serverDesiredMove.sqrMagnitude > 0.01f) ? effectiveSpeed : 0f;

        Vector3 move = serverDesiredMove * forward;

        if (controller != null)
        {
            controller.Move(move * Time.deltaTime);

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;
                networkIsJumpingNV.Value = false;
            }
        }
        else
        {
            transform.position += move * Time.deltaTime;
        }

        if (serverDesiredLook.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(serverDesiredLook);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private float CalculateSpeedOnServer(bool running, bool charging)
    {
        if (networkIsShootingNV.Value)
            running = false;

        if (charging) return 0f;

        return running ? baseSpeed * runMultiplier : baseSpeed;
    }

    private void UpdateAnimationsLocal()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", forwardSpeed);
        animator.SetBool("IsRunning", isRunning && !localAttackHeld);
        animator.SetBool("IsCharging", isCharging);
        animator.SetBool("IsJumping", localIsJumping);
        animator.SetBool("IsShooting", localAttackHeld);
    }

    private void UpdateAnimationsFromNetwork()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", currentRunningSpeedNV.Value);
        animator.SetBool("IsRunning", networkIsRunningNV.Value && !networkIsShootingNV.Value);
        animator.SetBool("IsCharging", networkIsChargingNV.Value);
        animator.SetBool("IsJumping", networkIsJumpingNV.Value);
        animator.SetBool("IsShooting", networkIsShootingNV.Value);
    }
}