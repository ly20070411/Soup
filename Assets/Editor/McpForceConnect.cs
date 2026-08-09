using System;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures Unity MCP HTTP bridge connects when the editor is ready.
/// </summary>
[InitializeOnLoad]
internal static class McpForceConnect
{
    private const string AttemptedKey = "McpForceConnect.Attempted";

    static McpForceConnect()
    {
        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
        EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
        EditorApplication.delayCall += TryConnect;
    }

    private static async void TryConnect()
    {
        if (SessionState.GetBool(AttemptedKey, false))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryConnect;
            return;
        }

        SessionState.SetBool(AttemptedKey, true);

        try
        {
            if (MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http))
            {
                Debug.Log("[McpForceConnect] HTTP bridge already running.");
                return;
            }

            if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
            {
                bool started = MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
                if (!started)
                {
                    Debug.LogWarning("[McpForceConnect] Failed to start local HTTP server.");
                    return;
                }

                for (int i = 0; i < 40 && !MCPServiceLocator.Server.IsLocalHttpServerReachable(); i++)
                    await System.Threading.Tasks.Task.Delay(250);
            }

            bool connected = await MCPServiceLocator.Bridge.StartAsync();
            Debug.Log(connected
                ? "[McpForceConnect] Unity MCP session connected."
                : "[McpForceConnect] Bridge.StartAsync returned false.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[McpForceConnect] {ex.Message}");
        }
    }
}
