using TMPro;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyItem : MonoBehaviour
{
    [SerializeField]
    private TMP_Text lobbyNameText;
    [SerializeField]
    private TMP_Text lobbyPlayerNumText;

    private LobbyList _lobbyList;
    private Lobby _lobby;

    public void Initialize(LobbyList list,  Lobby lobby)
    {
        // Setup UI text of the item:
        lobbyNameText.text = lobby.Name;
        lobbyPlayerNumText.text = $"{lobby.Players.Count} / {lobby.MaxPlayers}";

        _lobbyList = list;
        _lobby = lobby;
    }

    public void Join()
    {
        _lobbyList.JoinAsync(_lobby);
    }
}
