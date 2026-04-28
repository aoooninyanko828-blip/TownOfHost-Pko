using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TownOfHost; // LateTaskを使うため

namespace TownOfHost.Modules
{
    public static class Aiserver
    {
        private const string Url = "https://catwalk-skimming-lapel.ngrok-free.dev/ai";

        public static void Send(string prompt, byte senderId)
        {
            Logger.Info("[AI] Send called: " + prompt, "AI");
            Task.Run(async () =>
            {
                Logger.Info("[AI] Task.Run start", "AI");
                try
                {
                    using var client = new HttpClient();
                    // サーバーが落ちている時に長く固まらないよう、タイムアウトを10秒に短縮
                    client.Timeout = System.TimeSpan.FromSeconds(10);

                    string json = "{\"message\":\"" + EscapeJson(prompt) + "\"}";
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    Logger.Info("[AI] Sending request...", "AI");
                    var res = await client.PostAsync(Url, content);

                    // ★【超重要】通信が正常(200番台)じゃない場合、ここで弾く！（HTMLを解析させない）
                    if (!res.IsSuccessStatusCode)
                    {
                        Logger.Info($"[AI] Server Error: {res.StatusCode}", "AI");
                        _ = new LateTask(() => {
                            Main.MessagesToSend.Add(($"<color=#FFA500>ぴけおAI</color>: ふぁ…今寝てるぴけ。（※AIサーバーが起動していません）", byte.MaxValue, "ぴけおAI"));
                        }, 0.2f, "AI_Error_Task", true);
                        return; // これ以上下の処理（JSON解析）には進まずに終了
                    }

                    var body = await res.Content.ReadAsStringAsync();
                    var data = JObject.Parse(body);
                    string reply = data["reply"]?.ToString() ?? "AIエラー";

                    var sender = PlayerCatch.GetPlayerById(senderId);
                    string playerName = sender?.Data?.PlayerName ?? "Unknown";
                    bool isDead = sender == null || !sender.IsAlive();

                    // スパム判定のキックを避けるため、少し（0.2秒）遅らせてチャットを送信
                    _ = new LateTask(() => {
                        if (isDead)
                        {
                            foreach (var pc in PlayerCatch.AllPlayerControls)
                            {
                                if (pc.IsAlive()) continue;
                                Main.MessagesToSend.Add(($"{playerName}: {prompt}", pc.PlayerId, playerName));
                                // 送信者名はシンプルに "ぴけおAI" にする（カラーコードを入れると文字数オーバーでキックされるため）
                                Main.MessagesToSend.Add(($"<color=#FFA500>ぴけおAI</color>: {reply}", pc.PlayerId, "ぴけおAI"));
                            }
                        }
                        else
                        {
                            Main.MessagesToSend.Add(($"{playerName}: {prompt}", byte.MaxValue, playerName));
                            Main.MessagesToSend.Add(($"<color=#FFA500>ぴけおAI</color>: {reply}", byte.MaxValue, "ぴけおAI"));
                        }
                    }, 0.2f, "AI_Reply_Task", true);
                }
                catch (System.Exception e)
                {
                    Logger.Info("[AI] Exception: " + e.Message, "AI");
                    // 予期せぬエラーの時
                    _ = new LateTask(() => {
                        Main.MessagesToSend.Add(($"<color=#FFA500>ぴけおAI</color>: むにゃ…通信エラーだっぴけ…", byte.MaxValue, "ぴけおAI"));
                    }, 0.2f, "AI_Exception_Task", true);
                }
            });
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}