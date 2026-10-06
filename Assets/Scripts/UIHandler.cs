using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

public sealed class UIHandler : MonoBehaviour
{
    public static UIHandler Singleton { get; private set; }

    [Header("References")]
    [SerializeField] private TMP_Text ammoText;

    private ProjectileLaunch trackedWeapon;

    private void Awake()
    {
        if (Singleton == null)
        {
            Singleton = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Wait until the local player is spawned, then bind UI
        StartCoroutine(BindToLocalPlayer());
    }

    private IEnumerator BindToLocalPlayer()
    {
        // Wait for Netcode to spawn the player object
        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        NetworkObject playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;

        trackedWeapon = playerObj.GetComponent<ProjectileLaunch>();

        if (trackedWeapon == null)
        {
            Debug.LogError("UIHandler: No ProjectileLaunch found on local player.");
            yield break;
        }

        // Subscribe to ammo updates
        trackedWeapon.OnAmmoChanged += UpdateAmmoUI;

        // Force an initial refresh
        UpdateAmmoUI(trackedWeapon.GetCurrentAmmo(), trackedWeapon.GetMagazineSize());
    }

    private void UpdateAmmoUI(int current, int max)
    {
        if (ammoText == null) return;

        ammoText.text = current + "  /  " + max;
    }

    private void OnDestroy()
    {
        if (trackedWeapon != null)
            trackedWeapon.OnAmmoChanged -= UpdateAmmoUI;
    }
}