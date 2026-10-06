using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyList : MonoBehaviour
{
    [SerializeField] private LobbyItem itemPrefab;
    [SerializeField] private Transform lobbyItemParent;

    private bool isJoining = false;
    private bool isRefreshing = false;

    private void OnEnable()
    {
        RefreshList();
    }

    public async void RefreshList()
    {
        if (isRefreshing) { return; }

        isRefreshing = true;

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            // maximum number of Lobbys to query (e.g. for one page)
            options.Count = 25;
            options.Filters = new List<QueryFilter>()
            {
            // do not show lobbies which are full
            new QueryFilter(
                field: QueryFilter.FieldOptions.AvailableSlots,
                op: QueryFilter.OpOptions.GT,
                value: "0"
                ),
             // do not show lobbies which are locked
            new QueryFilter(
                field: QueryFilter.FieldOptions.IsLocked,
                op: QueryFilter.OpOptions.EQ,
                value: "0"
                )
            };

            QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync( options );

            // Clean the Scroll view
            foreach (Transform child in lobbyItemParent)
            {
                Destroy(child.gameObject);
            }

            foreach (Lobby lobby in lobbies.Results)
            {
                LobbyItem item = Instantiate(itemPrefab, lobbyItemParent);
                item.Initialize(this, lobby);
            }

        }
        catch (LobbyServiceException ex)
        {
            Debug.Log(ex);
        }

        isRefreshing = false;
    }


    public async void JoinAsync(Lobby lobby)
    {

        if (isJoining) { return; }

        isJoining = true;

        try
        {
            // the lobby retruned contains the data from the dictionary - the handed over ones not
            Lobby _joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
            string joinCode = _joinedLobby.Data["JoinCode"].Value;

            await ClientSingleton.Instance.GameManager.StartClientAsync(joinCode);
        }
        catch (LobbyServiceException ex)
        {
            Debug.Log(ex);
        }

        isJoining = false;
    }

}
