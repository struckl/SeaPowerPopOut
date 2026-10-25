// Updates an existing Workshop item through the running, logged-in Steam client (no password, no SteamCMD).
// Usage: WorkshopUpload <publishedFileId> <content folder> <change log file>
// Only the content and the change note are sent; title, description, tags, visibility and preview stay as they are.
using System;
using System.IO;
using System.Threading;
using Steamworks;

internal static class Program
{
    private const uint AppId = 1286220;

    private static int Main(string[] args)
    {
        if (args.Length != 3) { Console.Error.WriteLine("WorkshopUpload <publishedFileId> <folder> <changelog file>"); return 2; }
        ulong id = ulong.Parse(args[0]);
        string folder = Path.GetFullPath(args[1]);
        string note = File.ReadAllText(args[2]).Trim();

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "steam_appid.txt"), AppId.ToString());
        if (!SteamAPI.Init()) { Console.Error.WriteLine("SteamAPI.Init failed - Steam must be running and signed in."); return 1; }
        Console.WriteLine($"Signed in as {SteamFriends.GetPersonaName()}");

        UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(AppId), new PublishedFileId_t(id));
        if (!SteamUGC.SetItemContent(update, folder)) { Console.Error.WriteLine("SetItemContent failed."); return 1; }

        bool done = false; int code = 1;
        var result = CallResult<SubmitItemUpdateResult_t>.Create((r, ioFailure) =>
        {
            Console.WriteLine($"Result: {r.m_eResult} (io failure: {ioFailure}), needs legal agreement: {r.m_bUserNeedsToAcceptWorkshopLegalAgreement}");
            code = !ioFailure && r.m_eResult == EResult.k_EResultOK ? 0 : 1;
            done = true;
        });
        result.Set(SteamUGC.SubmitItemUpdate(update, note));

        int last = -1;
        while (!done)
        {
            SteamAPI.RunCallbacks();
            var status = SteamUGC.GetItemUpdateProgress(update, out ulong processed, out ulong total);
            int pct = total > 0 ? (int)(processed * 100 / total) : 0;
            if (pct != last) { Console.WriteLine($"{status} {pct}%"); last = pct; }
            Thread.Sleep(200);
        }
        SteamAPI.Shutdown();
        return code;
    }
}
