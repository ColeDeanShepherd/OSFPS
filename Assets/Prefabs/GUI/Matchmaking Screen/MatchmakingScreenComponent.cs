using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NetworkLibrary;
using System.Collections.Generic;
using UnityEngine.Networking.Match;
using UnityEngine.Networking;

public class MatchmakingScreenComponent : MonoBehaviour
{

    public List<MatchInfoSnapshot> LoadedMatches;
    public GameObject MatchesContainer;

    public void OnConnectToServerClick()
    {
        MatchInfoSnapshot matchInfoSnapshot = null;
        OsFps.Instance.NetworkMatch.JoinMatch(
            netId: matchInfoSnapshot.networkId, matchPassword: "", publicClientAddress: "",
            privateClientAddress: "", eloScoreForClient: 0, requestDomain: OsFps.MatchmakingRequestDomain,
            callback: OnMatchJoined
        );
    }
    public void OnStartDedicatedServerClick()
    {
        OsFps.Instance.NetworkMatch.CreateMatch(
            matchName: "", matchSize: Server.MaxPlayerCount, matchAdvertise: true,
            matchPassword: "", publicClientAddress: "", privateClientAddress: "", eloScoreForMatch: 0,
            requestDomain: OsFps.MatchmakingRequestDomain, callback: OnMatchCreated
        );
    }
    public void OnCancelClick()
    {
        OsFps.Instance.MenuStack.Pop();
    }

    private void Start()
    {
        UpdateUiForLoadedMatches();

        OsFps.Instance.NetworkMatch.ListMatches(
            startPageNumber: 0, resultPageSize: 100, matchNameFilter: "",
            filterOutPrivateMatchesFromResults: false, eloScoreTarget: 0,
            requestDomain: OsFps.MatchmakingRequestDomain, callback: OnLoadMatchesComplete
        );
    }

    private void OnLoadMatchesComplete(bool success, string extendedInfo, List<MatchInfoSnapshot> matches)
    {
        if (!success)
        {
            OsFps.Logger.LogError("Failed loading matches. " + extendedInfo);
            return;
        }

        LoadedMatches = (matches != null)
            ? matches
            : new List<MatchInfoSnapshot>();

        UpdateUiForLoadedMatches();
    }
    private void UpdateUiForLoadedMatches()
    {
        var containerTransform = MatchesContainer.GetComponent<RectTransform>();

        // clear list of servers
        foreach (RectTransform child in containerTransform)
        {
            Destroy(child.gameObject);
        }

        // add newly loaded servers
        if (LoadedMatches == null)
        {
            var text = Instantiate(OsFps.Instance.TextPrefab);
            text.GetComponent<Text>().text = "Loading...";

            text.GetComponent<RectTransform>().SetParent(containerTransform, false);
        }
        else if (LoadedMatches.Count == 0)
        {
            var text = Instantiate(OsFps.Instance.TextPrefab);
            text.GetComponent<Text>().text = "No servers found.";

            text.GetComponent<RectTransform>().SetParent(containerTransform, false);
        }
        else
        {
            foreach (var matchInfoSnapshot in LoadedMatches)
            {
                var serverRow = Instantiate(OsFps.Instance.ServerRowPrefab);
                serverRow.GetComponent<RectTransform>().SetParent(containerTransform, false);
            }
        }
    }

    private void OnMatchCreated(bool success, string extendedInfo, MatchInfo matchInfo)
    {
        if (!success)
        {
            OsFps.Logger.LogError("Failed creating a match. " + extendedInfo);
            return;
        }

        OsFps.Instance.MatchInfo = matchInfo;

        Utility.SetAccessTokenForNetwork(matchInfo.networkId, matchInfo.accessToken);

        OsFps.Instance.Server = new Server();
        OsFps.Instance.Server.Start();
        OsFps.Instance.MenuStack.Pop();
    }
    private void OnMatchJoined(bool success, string extendedInfo, MatchInfo matchInfo)
    {
        if (!success)
        {
            OsFps.Logger.LogError("Failed joining a match. " + extendedInfo);
            return;
        }

        Utility.SetAccessTokenForNetwork(matchInfo.networkId, matchInfo.accessToken);

        OsFps.Instance.EnteredClientIpAddressAndPort = matchInfo.address + ":" + matchInfo.port;

        SceneManager.sceneLoaded += OsFps.Instance.OnMapLoadedAsClient;
        SceneManager.LoadScene(OsFps.SmallMapSceneName);
        OsFps.Instance.MenuStack.Pop();
    }
}