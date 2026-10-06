using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterOutfitDatabase))]
[RequireComponent(typeof(CharacterVisualApplier))]
public class NetworkRandomOutfit : NetworkBehaviour
{
    private CharacterOutfitDatabase database;
    private CharacterVisualApplier applier;

    // The ONLY thing synchronized over network
    private NetworkVariable<int> outfitIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        database = GetComponent<CharacterOutfitDatabase>();
        applier  = GetComponent<CharacterVisualApplier>();
    }

    public override void OnNetworkSpawn()
    {
        outfitIndex.OnValueChanged += OnOutfitChanged;

        if (IsServer)
        {
            ChooseRandomOutfit();
        }

        // Apply for late joiners
        if (outfitIndex.Value >= 0)
        {
            ApplyCurrent();
        }
    }

    private void ChooseRandomOutfit()
    {
        if (database.Count == 0)
        {
            Debug.LogError("No outfits configured!");
            return;
        }

        outfitIndex.Value = Random.Range(0, database.Count);
    }

    private void OnOutfitChanged(int oldIndex, int newIndex)
    {
        ApplyCurrent();
    }

    private void ApplyCurrent()
    {
        var outfit = database.Get(outfitIndex.Value);
        applier.Apply(outfit);
    }
}
