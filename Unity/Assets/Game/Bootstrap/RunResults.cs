using System;
using Mongscape.Application;
using Mongscape.Domain;
using Mongscape.Infrastructure;

namespace Mongscape.Bootstrap
{
    // The end of a run: this PC's ranking, the weekly exercise record, the member's server save and the result numbers;
    // and the running ranking strip loaded at the start. GameBootstrap only wires it to the scene.
    public sealed class RunResults
    {
        public RankRace Race { get; } = new RankRace();
        public RankingEntry LastEntry { get; private set; }
        public int LastRank { get; private set; }
        public int LastTodayRank { get; private set; }
        public bool LastNewBest { get; private set; }
        public int LastBestScore { get; private set; } = -1;
        // Streak coins the last run earned (0 when none).
        public int LastStreakCoins { get; private set; }
        // The member's totals read after the last run was saved, or the save failed.
        public StatsReply LastStats { get; private set; }
        public bool LastStatsFailed { get; private set; }

        private readonly AccountFlow accounts;
        private readonly Func<PlayerProgressStore> store;
        private int raceTicket;
        private AccountSession Account => accounts.Account;
        private string ExerciseOwner => Account.IsMember ? Account.Id : "";

        public RunResults(AccountFlow accounts, Func<PlayerProgressStore> store) { this.accounts = accounts; this.store = store; }

        public void Clear()
        {
            LastEntry = null; LastStreakCoins = 0; LastRank = LastTodayRank = 0; LastBestScore = -1; LastNewBest = false; LastStats = null; LastStatsFailed = false;
        }
        // The run that just ended goes on this PC's ranking; a member's run is also saved on the account server,
        // which credits the coins. Guests collect no coins. kindSeconds: active seconds per course of this run.
        public void Record(RunSession session, PlayStyle mode, double[] kindSeconds)
        {
            if (session.IsTutorial) return;
            var Store = store();
            var now = DateTime.Now;
            LastEntry = new RankingEntry{Name=Account.Nickname,Score=session.Score.Score,Distance=session.Score.Distance,Coins=session.Score.Coins,
                Style=PlayStyleInfo.Label(mode),Mode=(int)mode,Kcal=PlayStyleInfo.UsesCamera(mode)?session.Workout.EstimatedKcal:0,
                Ticks=now.Ticks,AccountId=Account.IsMember?Account.Id:"",ScoreVersion=RunScore.Version};
            LastStats = null; LastStatsFailed = false;
            // Personal best per play style: a member's from /stats read before this save; none for guests.
            int previousBest = !Account.IsMember ? -1 : accounts.MemberBest(mode);
            LastNewBest = previousBest >= 0 && LastEntry.Score > previousBest; LastBestScore = previousBest > 0 ? previousBest : -1;
            LastRank = Store.Record(LastEntry, session.Workout.EstimatedKcal);
            // Weekly goal and streak: camera runs only. The streak is this PC's; a member's weekly minutes also go to the
            // server with the run (moderate_seconds). Members get the streak coins with this run.
            LastStreakCoins = PlayStyleInfo.UsesCamera(mode) ? Store.RecordExercise(ExerciseOwner, DateTime.UtcNow, session.Workout.ActiveSeconds, session.Workout.ModerateSeconds, kindSeconds) : 0;
            if (!Account.IsMember) LastStreakCoins = 0;
            LastTodayRank = Store.Board.RankOf(LastEntry, true, mode);
            if (!Account.IsMember) return;
            // The result screen shows the member's totals once the server has this run.
            string id = Account.Id;
            var record = AccountRun.Create(session, mode, session.Score.Coins + LastStreakCoins);
            accounts.Runs.Save(record, saved =>
            {
                if (!saved) { if (Account.Id == id) LastStatsFailed = true; return; }
                accounts.RefreshMemberStats(reply => { if (Account.Id != id) return; LastStats = reply != null && reply.ok ? reply : null; LastStatsFailed = LastStats == null; });
            });
        }
        // A guest's run was recorded as "게스트" and can still be named on the result screen.
        public bool CanNameLastRun => LastEntry != null && !Account.IsMember && string.IsNullOrEmpty(LastEntry.AccountId);
        public bool NameLastRun(string name) => CanNameLastRun && AccountSession.NicknameProblem(name) == null && store().Rename(LastEntry, name);

        public void ClearRace() { Race.Clear(); raceTicket++; }
        // The ranking strip's rows: this week's member board (best per member, without the player's own row) and this PC's guest
        // runs of the week, one row per name (its best), or with "내 기록만" only the member's own runs on this PC. Guests have no
        // "내 기록만" (every guest on a PC shares one empty id). The live score is the HUD's (QA 2026-11-25 M4, L5).
        public void LoadRace(PlayStyle mode, bool onlyMineSetting)
        {
            Race.Clear(); int mine = ++raceTicket;
            var now = DateTime.Now; var weekStart = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            string me = Account.IsMember ? Account.Id : "";
            bool onlyMine = onlyMineSetting && Account.IsMember;
            var rows = RankRace.LocalRows(store().Board.View(false, now, mode), weekStart, onlyMine, me);
            Race.Load(rows);
            if (onlyMine || accounts.Gateway == null) return;
            Action<LeaderboardReply> loaded = reply =>
            {
                if (mine != raceTicket || reply == null || !reply.ok || reply.entries == null) return;
                int own = reply.me != null ? reply.me.rank : 0;
                foreach (var e in reply.entries) if (e != null && e.rank != own) rows.Add(new RankRace.Row(e.name, e.score));
                // Loaded again before the run starts moving; the strip only shows once the run is under way.
                Race.Load(rows);
            };
            string token = Account.IsMember ? Account.Token : "";
            accounts.Gateway.WeekLeaderboard(token, mode, loaded);
        }
    }
}
