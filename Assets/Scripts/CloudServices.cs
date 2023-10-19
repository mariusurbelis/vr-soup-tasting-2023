using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.InputSystem;

public class CloudServices : MonoBehaviour
{
    private CloudServices _instance;

    internal async Task Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }

        if (!Utilities.CheckForInternetConnection()) return;

        Debug.Log("Initializing Unity Services...");

        await UnityServices.InitializeAsync();

        Debug.Log("Unity Services Initialized");

        AuthenticationService.Instance.ClearSessionToken();

        Debug.Log("Signing in anonymously...");

        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Already signed in as: " + AuthenticationService.Instance.PlayerId);
            AuthenticationService.Instance.SignOut();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Signing in...");
            await SignInAnonymously();
        }

        SubscribeToPlayerMessages();
        SubscribeToProjectMessages();

        RemoteConfigService.Instance.FetchCompleted += ApplyRemoteConfig;

        FetchRemoteConfig();
        FetchLeaderboard();
    }

    private struct UserAttributes
    {
    }

    private struct AppAttributes
    {
    }

    private async void FetchRemoteConfig()
    {
        await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
    }
    
    private async void FetchLeaderboard()
    {
        var scoresResponse = await LeaderboardsService.Instance.GetScoresAsync("scores");
        
        Debug.Log($"Got scores: {JsonConvert.SerializeObject(scoresResponse, Formatting.Indented)}");
    }

    void ApplyRemoteConfig(ConfigResponse configResponse)
    {
        // Conditionally update settings, depending on the response's origin:
        switch (configResponse.requestOrigin)
        {
            case ConfigOrigin.Default:
                Debug.Log("No settings loaded this session and no local cache file exists; using default values.");
                break;
            case ConfigOrigin.Cached:
                Debug.Log("No settings loaded this session; using cached values from a previous session.");
                break;
            case ConfigOrigin.Remote:
                Debug.Log("New settings loaded this session; update values accordingly.");
                break;
        }

        ConfigValues.SpawnDelay = RemoteConfigService.Instance.appConfig.GetFloat("spawnDelay");
        HoopData[] hoops =
            JsonConvert.DeserializeObject<HoopData[]>(RemoteConfigService.Instance.appConfig.GetJson("hoops"));

        // debug log all hoops
        foreach (var hoop in hoops)
        {
            Debug.Log($"Hoop {hoop.ID} at {hoop.X}, {hoop.Y}, {hoop.Z} with score {hoop.Score}");
        }

        GameManager.SpawnHoops(hoops);

        // assignmentId = RemoteConfigService.Instance.appConfig.assignmentId;
    }


    private void Update()
    {
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            SendMessageToAll("Hello from Unity!");
        }
    }

    public TextMeshProUGUI helloLabel;

    private async Task<string> SendMessageToAll(string message)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync("VRHandler", "SendAnnouncement",
            new Dictionary<string, object>() { { "message", message } }
        );

        return response;
    }

    public static async void CallScoreFunction(int hoopID, int score)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync("VRHandler", "AddScore",
            new Dictionary<string, object>()
            {
                { "hoopId", hoopID }, { "eventTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                { "hoopScore", score }
            });
    }

    public async void CallCloudCode()
    {
        // Call out to the RollDice endpoint in the HelloWorld module in Cloud Code
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync("VRHandler", "SayHello",
            new Dictionary<string, object>() { { "name", RandomText() } }
        );

        string RandomText()
        {
            var random = new System.Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[8];
            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        // Log the response of the module endpoint in console
        Debug.Log(response);
        helloLabel.text = response;
    }

    private async Task SignInAnonymously()
    {
        AuthenticationService.Instance.SignedIn += () =>
        {
            var playerId = AuthenticationService.Instance.PlayerId;

            Debug.Log("Signed in as: " + playerId);
        };
        AuthenticationService.Instance.SignInFailed += s =>
        {
            // Take some action here...
            Debug.Log(s);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    void SubscribeToPlayerMessages()
    {
        // Register callbacks, which are triggered when a player message is received
        var callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += @event => { PrintWireMessage(@event); };
        // callbacks.ConnectionStateChanged += @event =>
        // {
        //     Debug.Log(
        //         $"Got player subscription ConnectionStateChanged: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        // };
        // callbacks.Kicked += () => { Debug.Log($"Got player subscription Kicked"); };
        // callbacks.Error += @event =>
        // {
        //     Debug.Log($"Got player subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        // };
        CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
    }

    private class WireEvent
    {
        public string data_base64;
    }

    void SubscribeToProjectMessages()
    {
        var callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += @event => { PrintWireMessage(@event); };
        // callbacks.ConnectionStateChanged += @event =>
        // {
        //     Debug.Log(
        //         $"Got project subscription ConnectionStateChanged: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        // };
        callbacks.Kicked += () => { Debug.Log($"Got project subscription Kicked"); };
        callbacks.Error += @event =>
        {
            Debug.Log(
                $"Got project subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        };

        CloudCodeService.Instance.SubscribeToProjectMessagesAsync(callbacks);
    }

    private static void PrintWireMessage(IMessageReceivedEvent @event)
    {
        string jsonMessage = JsonConvert.SerializeObject(@event, Formatting.Indented);
        WireEvent deserializedEvent = JsonConvert.DeserializeObject<WireEvent>(jsonMessage);
        string data = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(deserializedEvent.data_base64));

        Debug.Log($"Got project subscription at {DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK")} Message: {data}");
    }
}