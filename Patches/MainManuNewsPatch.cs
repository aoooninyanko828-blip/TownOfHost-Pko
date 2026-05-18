//TOH_Yを参考にさせて貰いました ありがとうございます
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data.Player;
using Assets.InnerNet;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
using TownOfHost;
using UnityEngine.Networking;

[HarmonyPatch]
public class ModNewsHistory
{
    public static List<ModNews> AllModNews = new();
    public static List<ModNews> JsonAndAllModNews = new();
    public static void Init()
    {
        {
            //リンクはこうやるらしい。<nobr><link=\"URL\">Text</nobr></link>
            /*　　テンプレート
            {
                var news = new ModNews
                {
                    Number = 100002,
                    Title = "text",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.23.11.39</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.23.11.39</color>",
                    Text = "text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text"
                    ,
                    Date = "2026-4-20T00:00:00Z"
                };
                AllModNews.Add(news);
            }*/
            {
                var news = new ModNews
                {
                    Number = 100073,
                    Title = "ぬーん",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.28.14.60</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.28.14.60</color>",
                    Text = "・未完成の役職の削除\n"
                    +"・新役職スモークメーカーの追加\n"
                    ,
                    Date = "2026-5-17"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100072,
                    Title = "そーりー",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.28.14.59</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.28.14.59</color>",
                    Text = "・ホストがチャット見えなかったバグの修正\n",
                    Date = "2026-5-16"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100070,
                    Title = "波動砲シェリフ？なんだそりゃ　まだ完成してねえよ!!!",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.27.13.51</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.27.13.51</color>",
                    Text = "・New役職　ヒッチハイカーの追加\n"
                    + "・New属性　ジャンボの追加\n"
                    + "・ロケットの追加 From:SuperNewRoles\n"
                    + "・ニムロッドの追加 From:TownOfHOst-Y\n"
                    + "・爆ぜ師の追加 From:SuperNewRoles\n"
                    + "・自殺願望者の追加 From:SuperNewRoles\n"
                    + "・神がタスク終わってなくても勝利できたバグの修正\n"
                    + "・賢者のクールタイムがおかしかったバグの修正\n"
                    + "・波動砲、波動砲ジャッカル、波動砲シェリフのロワーテクストの改行\n"
                    + "・シーアの霊魂が2個以上になると正しく表示されないバグの修正\n"
                    + "・ペットがつかなかったバグの修正\n"
                    + "・忘却者のバグ修正\n"
                    + "・シーアの霊魂数表示を削除\n"
                    + "・弾の軽量化\n"
                    + "・弾のワープをちょい修正\n"
                    + "・タイムリーパーのワープをちょい修正\n"
                    + "・ヒッチハイカーのワープをちょい修正\n"
                    + "・賢者、ニムロッド、爆ぜ師、ヒッチハイカー翻訳\n"
                    + "・ジェスタータスクできないバグの修正\n"
                    + "・ニムロッド修正\n"
                    ,
                    Date = "2026-5-09"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100069,
                    Title = "なんか皆使ってくれてるから4週間後に一気にアプデしようかと思ったけどあげちゃうわ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.26.13.50</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.26.13.50</color>",
                    Text = "・TOHKのアップデートに対応\n"
                    + "・波動砲ジャッカルのバグ修正\n"
                    + "・波動砲のチャージ中の★が正常に表示されなかったバグの修正\n"
                    + "・波動砲ジャッカルの軽量化\n"
                    + "・チャッターのバグ修正\n"
                    + "・ダブルキラーのバグ修正\n"
                    + "・毒入りパン屋のバグ修正\n"
                    + "・翻訳のバグ修正\n"
                    + "・配信者向けオプションの追加\n"
                    + "・モデレーターの修正\n"
                    + "・ジェスターのバグ修正\n"
                    + "・サンタにナイスゲッサーの追加\n"
                    + "・サンタにオプションの追加\n"
                    + "・pkoコマンドを制御するオプションの追加\n"
                    + "・/cmd hに新しいコマンドを追加\n"
                    + "・id重複の修正\n"
                    + "・New デバフアドオンの追加「スタミナー」\n"
                    + "・プクプクの翻訳\n"
                    + "・プクプクのキルされてから何ターンまで毒復讐を有効かのオプションの追加\n"
                    + "・プクプクにタスク完了時の能力のバリエーションの追加\n"
                    + "・/cmd s rしたルールはアモアスを閉じても残るように変更\n"
                    + "・ピンの画像の変更\n"
                    + "・侍の仕様変更\n"
                    ,
                    Date = "2026-5-04"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100068,
                    Title = "4つめの新役だぜ！",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.12.44</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.12.44</color>",
                    Text = "・侍の追加 From:SuperNewRoles\n"
                    + "・TOHKのアプデに対応\n"
                    + "・賢者の追加 From:SuperNewRoles (現在未翻訳、あと能力使用後も賢者ステップ健在()\n"
                    + "・新役チャッターの追加\n"
                    ,
                    Date = "2026-4-30"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100067,
                    Title = "波動砲、波動砲ジャッカルを超超超！大幅軽量化しました！！！",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.43</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.43</color>",
                    Text = "・あとシーアの霊魂が正常に表示されるようになりました！(みっちーくんあざす)\n"
                    ,
                    Date = "2026-4-29"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100067,
                    Title = "Zzz...",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.42</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.42</color>",
                    Text = "・忘却者の修正\n"
                    + "・フレンド招待機能の削除\n"
                    + "・波動砲、波動砲ジャッカルの軽量化\n"
                    ,
                    Date = "2026-4-29"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100066,
                    Title = "だいぶバグが減ってきた実感があるじぇ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.41</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.41</color>",
                    Text = "・パブロフの犬のバグ修正\n"
                    + "・ナイスゲッサーのバグ修正\n"
                    + "・イーターのバグ修正\n"
                    + "モデレーターの追加仕様\n"
                    + "・クレジット追加\n"
                    ,
                    Date = "2026-4-29"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100065,
                    Title = "１日に3回もアプデは流石に草ｗ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.40</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.40</color>",
                    Text = "・サンタの役職説明に不備があったので修正\n"
                    + "・役職ガイドの更新\n"
                    + "・自動スタート時暗転するバグの修正\n"
                    + "ぴけおAIオンライン対応しました(つまり開発者以外もぴけおAI使えるよってこと"
                    + "↑ただし24時間営業ではない　使いたい時しぇとこさんに言っていただければ起動いたします\n"
                    + "・イビルゲッサーに数字が見えないバグの修正\n"
                    ,
                    Date = "2026-4-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100064,
                    Title = "ぷれいしていただきありがとうございます:)",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.24.11.40</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.24.11.40</color>",
                    Text = "・モデレーターの修正\n"
                    + "・波動砲の★が正しく表示されないバグの修正(これもみっちーのせいです、許しません^^)\n"
                    + "・波動砲、波動砲ジャッカルのチャージ中とビーム中の★の色を自分の体の色に仕様変更(これで詰めやすくなったね!)"
                    + "↑それに伴い役職説明も変更したぜーい\n"
                    + "・イビルゲッサー、ナイスゲッサーの翻訳\n"
                    + "・新しいボタンの作成　※まだ未完成\n"
                    ,
                    Date = "2026-4-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100063,
                    Title = "Pkoってぴけおって読むらしいぜ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.23.11.39</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.23.11.39</color>",
                    Text = "・チェイサーの追加 From:TownOfHost_Y\n"
                    + "・/cmd hができないバグの修正(ホストも)\n"
                    + "・ダブルキラーのバグ修正...?\n"
                    + "・波動砲、波動砲ジャッカルの軽量化...?(気持ち程度かもしんねー\n"
                    ,
                    Date = "2026-4-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100062,
                    Title = "<size=70%>このModに必要なWinnerのシステムを完全に理解したわけでもないのに\r\n「いらないよ」と言ってしまい勝利システムに不具合が発生していました。\r\n申し訳ございませんでした。byみっちー</size>",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.23.11.38</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.23.11.38</color>",
                    Text = "・波動砲のバグ修正\n"
                    + "・自動スタートの調整\r\n"
                    + "・skむずかしいと言われたので村長、陰陽師、波動砲ジャッカルのskの時間を3秒から1.5秒に短縮、範囲1.0→1.5に変更\n"
                    + "\r\nバニラの試合結果の表示\r\n↑多分すぐ消す\r\n\n"
                    + "・マグロの勝利できなかったバグの修正\n"
                    + "・スペランカーの勝利できなかったバグの修正\n"
                    + "・陰陽師バグ修正\n"
                    + "・神バグ修正\n"
                    + "・イビルゲッサーバグ修正(一部)n"
                    ,
                    Date = "2026-04-27"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100079,
                    Title = "Kのアプデ対応は地味に大変㌨;-;",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.27.13.54</color>",
                    ShortTitle = "<color=#FF9631>◆TOH-Pko v4.27.13.54</color>",
                    Text = "TOHKのアップデートに対応したよ！！！！！\n"
                    + "ところで次バージョン何出せばいいの??\n"
                    + "みんな役提案とか待ってるよ！"
                    ,
                    Date = "2026-05-12"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100080,
                    Title = "マッチメイキング実装予定だった。",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.27.13.57</color>",
                    ShortTitle = "<color=#FF9631>◆TOH-Pko v4.27.13.57</color>",
                    Text = "<size=80%>"
                    + "マッチメイキング機能実装予定のため公開ルームにすることができるようになっていますが、\n"
                    + "バニラユーザーからは実際に公開ルームとして反映されていないのでバニラのマッチメイキングからは参加することができなくなっています。\n"
                    + "<size=125%>【バグ修正】</size>\n"
                    + "・ジャンボの属性付与infoが表示されなかった問題の修正\n"
                    + "\n<size=125%>【仕様変更】</size>\n"
                    + "・コマンド実行結果の分割量の変更\n"
                    + "\n<size=125%>【設定追加】</size>\n"
                    + " ・陰陽師/式神の秘匿チャットの追加\n"
                    + "\n<size=125%>【追加役職】</size>\n"
                    + "<size=100%>Ⓝバットガール/Batgirl</size>\n"
                    ,
                    Date = "2026-05-14"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100081,
                    Title = "ほくほくのさつまいもはいかが？",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.29.14.61</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.29.14.61</color>",
                    Text = "・新役職　スタンドマスター、スタンドの追加\n"
                    + "・さつまといもの追加 From:SuperNewRoles\n"
                    + "・シャイボーイを第三陣営に変更\n"
                    + "・イビルゲッサーの会議でidが表示されないバグの修正\n"
                    + "・ヒッチハイカーちょい修正\n"
                    + "・ナイストラッパー仕様修正更\n"
                    + "・自殺願望者の翻訳\n"
                    + "・画像の変更\n"
                    + "・その他の変更\n"
                    ,
                    Date = "2026-5-18"
                };
                AllModNews.Add(news);
            }
            AnnouncementPopUp.UpdateState = AnnouncementPopUp.AnnounceState.NotStarted;
        }
    }
    //ここもTownOfHost_Y様を参考に..!
    public const string ModNewsURL = "";
    static bool downloaded = false;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void StartPostfix(MainMenuManager __instance)
    {
        static IEnumerator FetchModNews()
        {
            if (downloaded)
            {
                yield break;
            }
            downloaded = true;
            var request = UnityWebRequest.Get(ModNewsURL);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
            {
                downloaded = false;
                TownOfHost.Logger.Info("ModNews Error Fetch:" + request.responseCode.ToString(), "ModNews");
                yield break;
            }
            var json = JObject.Parse(request.downloadHandler.text);
            for (var news = json["News"].First; news != null; news = news.Next)
            {
                JsonModNews n = new(
                    int.Parse(news["Number"].ToString()), news["Title"]?.ToString(), news["Subtitle"]?.ToString(), news["Short"]?.ToString(),
                    news["Body"]?.ToString(), news["Date"]?.ToString());
            }
        }
        __instance.StartCoroutine(FetchModNews().WrapToIl2Cpp());
    }

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    public static bool SetModAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.Count < 1)
        {
            Init();
            AllModNews.Do(n => JsonAndAllModNews.Add(n));
            JsonAndAllModNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });
        }

        List<Announcement> FinalAllNews = new();
        JsonAndAllModNews.Do(n => FinalAllNews.Add(n.ToAnnouncement()));
        foreach (var news in aRange)
        {
            if (!JsonAndAllModNews.Any(x => x.Number == news.Number))
                FinalAllNews.Add(news);
        }
        FinalAllNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });

        aRange = new(FinalAllNews.Count);
        for (int i = 0; i < FinalAllNews.Count; i++)
            aRange[i] = FinalAllNews[i];

        return true;
    }
}