using System;
using Mongscape.Application;
using Mongscape.Domain;
using Mongscape.Infrastructure;
using Mongscape.Presentation;
using UnityEngine;

namespace Mongscape.Bootstrap
{
    // Account and face login: the account server (or a test fake), the login gate's server check, face sign-in and sign-up,
    // the member's profile, stats and deletion, and the expired-session sign-out. GameBootstrap only wires it to the scene.
    public sealed class AccountFlow
    {
        // A member call answered 401: that token expired on the server. The member is signed out here (no /logout) and the
        // login gate opens with the notice; during a run (and its result) this waits until the menu is back.
        public const string ExpiredNotice = "로그인이 만료되었어요. 다시 로그인해 주세요.";
        // The account (member or guest) is the only source of the current player.
        public AccountSession Account { get; } = new AccountSession();
        public IAccountGateway Gateway { get; private set; }
        public AccountRunUploader Runs { get; }
        public Texture2D Portrait { get; private set; }
        public bool ServerChecking { get; private set; }
        public string ServerProblem { get; private set; } = "";
        public string LoginNotice { get; private set; } = "";
        public bool IdentityBusy => identity.Busy;
        public string IdentityMessage => identity.Message;
        // Raised when another member (or back to a guest) takes over; coin or setting updates do not raise it.
        public event Action PlayerSwitched;

        private readonly LocalProfileClient identity;
        private readonly Func<PlayerProgressStore> store;
        // Whether the gate may open right now (title screen, not loading), and how to open it after an expiry.
        private readonly Func<bool> canShowGate;
        private readonly Action showExpiredGate;
        private Action<Action<string[]>> captureFaces;
        private string shownAccount = "", expiredToken, memberStatsId = "";
        private int serverTicket;
        // The member's /stats (best score per play style), read before a run and after its save.
        private StatsReply memberStats;

        public AccountFlow(LocalProfileClient identity, Func<Texture> camera, Func<PlayerProgressStore> store, Func<bool> canShowGate, Action showExpiredGate)
        {
            this.identity = identity; this.store = store; this.canShowGate = canShowGate; this.showExpiredGate = showExpiredGate;
            Gateway = new ExpiryWatchingGateway(identity, SessionExpired); Runs = new AccountRunUploader(Account, Gateway);
            captureFaces = done => identity.CaptureFaces(camera(), done);
            Account.Changed += AccountChanged;
        }

        // Tests and tools: another account server (or a fake) and a face capture without a webcam.
        public void UseServices(IAccountGateway accountGateway, Action<Action<string[]>> faces)
        {
            if (accountGateway != null) { Gateway = new ExpiryWatchingGateway(accountGateway, SessionExpired); Runs.Gateway = Gateway; serverTicket++; ServerChecking = false; ServerProblem = ""; }
            if (faces != null) captureFaces = faces;
        }
        public void ClearLoginNotice() => LoginNotice = "";

        private void AccountChanged()
        {
            string id = Account.IsMember ? Account.Id : "";
            if (id == shownAccount) return;
            shownAccount = id;
            if (!Account.IsMember) ClearPortrait();
            // The HUD best and the next result compare with this member's /stats.
            memberStats = null; memberStatsId = "";
            if (Account.IsMember && Gateway != null) RefreshMemberStats();
            PlayerSwitched?.Invoke();
        }
        private void SignedInReply(AccountReply reply)
        {
            if (reply == null || !reply.HasProfile) return;
            ClearPortrait();
            // The face crop only exists in this reply; it becomes the badge and is never stored.
            if (!string.IsNullOrEmpty(reply.avatar))
            {
                Portrait = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try { if (!Portrait.LoadImage(Convert.FromBase64String(reply.avatar))) ClearPortrait(); } catch { ClearPortrait(); }
            }
            shownAccount = ""; LoginNotice = ""; expiredToken = null;
            Account.SignInMember(reply.profile, reply.token);
        }
        public void ClearPortrait() { if (Portrait != null) UnityEngine.Object.Destroy(Portrait); Portrait = null; }
        // Back to a guest at once; /logout is sent without waiting.
        private void SignOutMember()
        {
            if (!Account.IsMember) return;
            string token = Account.Token;
            Account.SignOut();
            if (!string.IsNullOrEmpty(token)) Gateway.Logout(token, null);
        }
        // Members are signed out when the game closes; the request is not awaited.
        public void LogoutOnQuit() { if (Account.IsMember && !string.IsNullOrEmpty(Account.Token)) Gateway?.Logout(Account.Token, null); }
        // Only the signed-in member's own token counts; a late 401 for an earlier member is ignored.
        private void SessionExpired(string token)
        {
            if (!Account.IsMember || token != Account.Token) return;
            expiredToken = token;
            if (canShowGate()) ApplyExpiry();
        }
        public bool ExpiryPending => expiredToken != null && Account.IsMember && expiredToken == Account.Token;
        public void ApplyExpiry()
        {
            bool expired = ExpiryPending; expiredToken = null;
            if (!expired) return;
            Account.SignOut(); LoginNotice = ExpiredNotice;
            showExpiredGate();
        }

        // The member's best score for one play style from the last /stats; -1 when it is not known.
        public int MemberBest(PlayStyle mode)
        {
            var stats = memberStats;
            return Account.IsMember && stats != null && memberStatsId == Account.Id && stats.best_score != null && stats.best_score.Length > (int)mode ? stats.best_score[(int)mode] : -1;
        }
        // Reads /stats for the signed-in member and keeps it for the HUD best and the next result.
        public void RefreshMemberStats(Action<StatsReply> done = null)
        {
            if (!Account.IsMember || string.IsNullOrEmpty(Account.Token)) { done?.Invoke(null); return; }
            string id = Account.Id;
            Gateway.Stats(Account.Token, reply =>
            {
                if (reply != null && reply.ok && Account.IsMember && Account.Id == id)
                {
                    memberStats = reply; memberStatsId = id;
                    // The weekly goal bar follows the server's sum over every PC of the member.
                    store().Exercise.SyncWeek(id, DateTime.UtcNow, reply.week_moderate_seconds);
                }
                done?.Invoke(reply);
            });
        }
        // Guests keep settings in session memory; a member's settings also go to the server.
        public void SaveSettings(AccountSettings settings)
        {
            Account.UpdateSettings(settings);
            if (!Account.IsMember) return;
            string id = Account.Id;
            Gateway.UpdateProfile(Account.Token, AccountProfileUpdate.From(Account.Settings), reply => { if (reply.HasProfile && reply.profile.id == id) Account.Apply(reply.profile); });
        }
        // A member's profile remembers the finished tutorial.
        public void MarkTutorialDone()
        {
            if (!Account.IsMember) return;
            string id = Account.Id; Account.SetTutorialDone(true);
            Gateway.UpdateProfile(Account.Token, new AccountProfileUpdate { TutorialDone = true }, reply => { if (reply != null && reply.HasProfile && reply.profile.id == id && reply.profile.tutorial_done) Account.Apply(reply.profile); });
        }
        // Members buy on the server and take its balance and unlocked items; guests have no coins.
        public void Unlock(ItemKind kind, Action<bool, string> done)
        {
            if (!Account.IsMember) { done?.Invoke(false, "회원가입하면 코인을 모아 아이템을 해금할 수 있어요"); return; }
            string id = Account.Id;
            Gateway.Buy(Account.Token, kind, reply =>
            {
                if (reply != null && reply.HasProfile && reply.profile.id == id && Account.Apply(reply.profile)) { done?.Invoke(true, ""); return; }
                NoteServer(reply);
                string reason = reply == null || !string.IsNullOrEmpty(reply.message) || reply.status == 0 ? ServerReason(reply)
                    : reply.status == 409 ? "이미 해금한 아이템입니다." : "코인이 부족합니다.";
                done?.Invoke(false, reason);
            });
        }

        // Asked each time the login gate opens; until the server answers, face login and sign-up wait. Only the latest answer counts.
        public void CheckServer()
        {
            int mine = ++serverTicket; ServerChecking = true;
            Gateway.Health(reply => { if (mine != serverTicket) return; ServerChecking = false; ServerProblem = reply != null && reply.ok ? "" : ServerReason(reply); });
        }
        public void CheckNickname(string name, Action<bool, string> done)
        {
            string problem = AccountSession.NicknameProblem(name);
            if (problem != null) { done?.Invoke(false, problem); return; }
            Gateway.NameCheck(name.Trim(), reply =>
            {
                if (reply == null || !reply.ok) { NoteServer(reply); done?.Invoke(false, ServerReason(reply)); return; }
                done?.Invoke(reply.available, reply.available ? "" : string.IsNullOrEmpty(reply.message) ? "사용할 수 없는 닉네임입니다." : reply.message);
            });
        }
        public void FaceLogin(Action<bool> done)
        {
            if (IdentityBusy || Account.IsMember) return;
            captureFaces(images => Gateway.Login(images, reply => FaceReply(reply, done)));
        }
        // Sign-up: the nickname and settings chosen in the sign-up steps, not the guest's.
        public void FaceEnroll(string nickname, AccountSettings settings, Action<bool> done)
        {
            if (IdentityBusy || Account.IsMember || AccountSession.NicknameProblem(nickname) != null) return;
            settings ??= new AccountSettings();
            captureFaces(images => Gateway.Enroll(new AccountEnrollment { name = nickname.Trim(), consent = true, images = images, weight = settings.Weight, style = (int)settings.Style, goal = settings.Goal }, reply => FaceReply(reply, done)));
        }
        private void FaceReply(AccountReply reply, Action<bool> done)
        {
            NoteServer(reply); SignedInReply(reply);
            done?.Invoke(reply != null && reply.HasProfile && Account.IsMember);
        }
        // A request that could not reach the server disables face login and sign-up on the gate until the next check.
        // An expired member token (401) is handled for every call by ExpiryWatchingGateway (SessionExpired).
        private void NoteServer(AccountResult reply) { if (reply != null && !reply.ok && reply.status == 0) ServerProblem = ServerReason(reply); }
        private static string ServerReason(AccountResult reply) => reply != null && !string.IsNullOrEmpty(reply.message) ? reply.message : "얼굴 서버에 연결할 수 없습니다.";
        public void FaceLogout(bool delete)
        {
            if (IdentityBusy || !Account.IsMember) return;
            if (!delete) { SignOutMember(); return; }
            DeleteAccount(null);
        }
        // 내 정보: nickname and settings in one /profile/update; the session changes only after the server accepted them.
        public void SaveMemberProfile(string nickname, AccountSettings settings, Action<bool, string> done)
        {
            if (!Account.IsMember) { done?.Invoke(false, "회원만 저장할 수 있습니다."); return; }
            string problem = AccountSession.NicknameProblem(nickname);
            if (problem != null) { done?.Invoke(false, problem); return; }
            var update = AccountProfileUpdate.From(settings ?? Account.Settings);
            string name = nickname.Trim(), id = Account.Id;
            if (name != Account.Nickname) update.Name = name;
            Gateway.UpdateProfile(Account.Token, update, reply =>
            {
                if (reply != null && reply.HasProfile && reply.profile.id == id && Account.Apply(reply.profile)) { done?.Invoke(true, ""); return; }
                NoteServer(reply);
                done?.Invoke(false, reply != null && reply.status == 409 && string.IsNullOrEmpty(reply.message) ? "이미 사용 중인 닉네임입니다." : ServerReason(reply));
            });
        }
        // Server totals for 내 정보; null when there is no member or no token.
        public void MemberStats(Action<StatsReply> done)
        {
            if (!Account.IsMember || string.IsNullOrEmpty(Account.Token)) { done?.Invoke(null); return; }
            Gateway.Stats(Account.Token, done);
        }
        // 회원 랭킹 for anyone; a member's token adds their own row ("me").
        public void MemberLeaderboard(PlayStyle mode, bool today, Action<LeaderboardReply> done)
        {
            if (Gateway == null) { done?.Invoke(new LeaderboardReply { message = "서버에 연결할 수 없어요" }); return; }
            Gateway.Leaderboard(Account.IsMember ? Account.Token : "", mode, today, done);
        }
        // Account deletion (face, profile and runs on the server, and the member's rows on this PC's ranking); the player becomes a guest.
        public void DeleteAccount(Action<bool, string> done)
        {
            if (IdentityBusy || !Account.IsMember) { done?.Invoke(false, "회원만 탈퇴할 수 있습니다."); return; }
            string id = Account.Id;
            Gateway.Delete(Account.Token, reply =>
            {
                bool ok = reply != null && reply.ok;
                if (ok) { store().RemoveAccount(id); if (Account.IsMember && Account.Id == id) Account.SignOut(); }
                else NoteServer(reply);
                done?.Invoke(ok, ok ? "" : ServerReason(reply));
            });
        }
    }
}
