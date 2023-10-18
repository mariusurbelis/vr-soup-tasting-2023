using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.InputSystem;

public class CloudServices : MonoBehaviour
{
    internal async Task Awake()
    {
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
    }

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            BroadcastMessage("Hello from Unity!");
        }
    }

    public TextMeshProUGUI helloLabel;

    private async Task<string> BroadcastMessage(string message)
    {
        var response = await CloudCodeService.Instance.CallModuleEndpointAsync("VRHandler", "SendAnnouncement",
            new Dictionary<string, object>() { { "message", message } }
        );
        
        return response;
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
        callbacks.MessageReceived += @event =>
        {
            PrintWireMessage(@event);
        };
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