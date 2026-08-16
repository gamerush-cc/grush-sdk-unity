using System.Threading.Tasks;
using GRushSdk;
using UnityEngine;

public class GRushScoreAttackSample : MonoBehaviour
{
    private const string BestScoreKey = "sample.scoreAttack.best";

    private GRushPlayer player;
    private string status = "Connecting to GameRush...";
    private int score;
    private int best;
    private float remaining;
    private bool running;

    private async void Start()
    {
        best = PlayerPrefs.GetInt(BestScoreKey, 0);
        await RefreshPlayerAsync();
    }

    private async Task RefreshPlayerAsync()
    {
        var result = await GRush.Player.GetSelfAsync();
        if (!result.Ok)
        {
            status = GRush.IsAvailable ? result.Message : "Running outside GameRush.";
            return;
        }
        player = result.Value;
        status = player.IsGuest ? "Playing as a guest." : "Signed in.";
    }

    private async void RequestProfile()
    {
        status = "Waiting for the GameRush consent dialog...";
        var result = await GRush.Player.RequestProfileAsync();
        if (!result.Ok)
        {
            status =
                result.Code == GRushErrorCode.ConsentDeclined
                    ? "Profile sharing was declined."
                    : result.Message;
            return;
        }
        player = result.Value;
        status = "Profile shared.";
    }

    private void Update()
    {
        if (!running)
        {
            return;
        }
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            running = false;
            remaining = 0f;
            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt(BestScoreKey, best);
                PlayerPrefs.Save();
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 380, 320));
        GUILayout.Label("GameRush Score Attack");
        GUILayout.Label(status);
        GUILayout.Label("Pseudo ID: " + (string.IsNullOrEmpty(player.PseudoId) ? "-" : player.PseudoId));
        GUILayout.Label(
            "Display name: "
                + (player.ProfileConsent && !string.IsNullOrEmpty(player.DisplayName)
                    ? player.DisplayName
                    : "(not shared)")
        );

        if (!player.ProfileConsent && !player.IsGuest && GUILayout.Button("Share my profile"))
        {
            RequestProfile();
        }

        GUILayout.Space(12);
        GUILayout.Label("Score: " + score + "    Best: " + best);
        GUILayout.Label("Time left: " + Mathf.CeilToInt(remaining));

        if (!running && GUILayout.Button("Start 10 second run"))
        {
            score = 0;
            remaining = 10f;
            running = true;
        }
        if (running && GUILayout.Button("Tap for a point"))
        {
            score += 1;
        }
        GUILayout.EndArea();
    }
}
