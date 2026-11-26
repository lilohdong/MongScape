using System;
using System.Collections.Generic;
using System.Globalization;
using Mongscape.Domain;

namespace Mongscape.Application
{
    public enum AccountKind { Guest, Member }

    // Settings that follow the player: the member's server profile, or session memory for a guest.
    [Serializable]
    public sealed class AccountSettings
    {
        public const double DefaultWeight = 65, DefaultGoal = 50;
        public double Weight = DefaultWeight;
        public PlayStyle Style = PlayStyle.FullBody;
        public double Goal = DefaultGoal;
        public AccountSettings Clone() => (AccountSettings)MemberwiseClone();
    }

    // Who plays on this PC right now. A guest keeps everything in memory; a member mirrors the server profile.
    // Signing out (or quitting) always returns to a fresh guest; there is no automatic login.
    public sealed class AccountSession
    {
        public const string GuestName = "게스트";
        public const int MaxNickname = 10;
        private readonly TraitLevels traits = new TraitLevels();
        private AccountSettings settings = new AccountSettings();
        private PlayerProfile profile;
        public AccountKind Kind { get; private set; } = AccountKind.Guest;
        public bool IsMember => Kind == AccountKind.Member;
        public string Id { get; private set; } = "";
        public string Token { get; private set; } = "";
        public string Nickname { get; private set; } = GuestName;
        public AccountSettings Settings => settings.Clone();
        // Rescued monkeys the member holds (the server's coins); a guest has none and owns no trait.
        public int Monkeys { get; private set; }
        public TraitLevels Traits => traits;
        public bool TutorialDone { get; private set; }
        public event Action Changed;

        public void SignInMember(AccountProfile account, string token)
        {
            if (account == null || string.IsNullOrEmpty(account.id)) return;
            Kind = AccountKind.Member; Id = account.id; Token = token ?? "";
            Load(account); Notify();
        }
        // Refreshes the member from a server reply; replies for another account are ignored.
        public bool Apply(AccountProfile account)
        {
            if (!IsMember || account == null || account.id != Id) return false;
            Load(account); Notify(); return true;
        }
        public void SetMonkeys(int monkeys)
        {
            if (!IsMember) return;
            Monkeys = Math.Max(0, monkeys); Notify();
        }
        public void UpdateSettings(AccountSettings value)
        {
            if (value == null) return;
            settings = Sanitize(value.Weight, (int)value.Style, value.Goal); Notify();
        }
        // A guest renames in memory; a member only after the server accepted the name.
        public void SetNickname(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            Nickname = name.Trim(); Notify();
        }
        public void SetTutorialDone(bool done) { TutorialDone = done; Notify(); }
        public void SignOut()
        {
            Kind = AccountKind.Guest; Id = Token = ""; Nickname = GuestName; TutorialDone = false;
            settings = new AccountSettings(); Monkeys = 0; traits.Clear(); Notify();
        }

        // The account as the existing screens see it: name, weight text, default style and calorie goal.
        public PlayerProfile Profile => profile ??= new PlayerProfile
        {
            Name = Nickname, Weight = settings.Weight.ToString("0.##", CultureInfo.InvariantCulture), Style = settings.Style, CalorieGoal = settings.Goal,
        };
        // The traits a run plays with (a guest plays without any), and the items they place on the map.
        public RunTraits TraitsForRun() => IsMember ? traits.ForRun() : RunTraits.None;
        public List<ItemKind> RunItems() => ItemCatalog.Available(TraitsForRun());
        // Server nickname rule (storage.clean_name): 1-10 code points after trimming (Python len()), and no character of
        // a Unicode "C" category (control, format such as U+200B, private use, unassigned, lone surrogate); null when acceptable.
        public static string NicknameProblem(string name)
        {
            string text = name?.Trim() ?? "";
            if (text.Length == 0) return "닉네임을 입력해 주세요.";
            if (CodePoints(text) > MaxNickname) return "닉네임은 " + MaxNickname + "자 이하로 입력해 주세요.";
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
                if (Forbidden(CharUnicodeInfo.GetUnicodeCategory(text, i))) return "닉네임에 쓸 수 없는 문자가 있습니다.";
            return null;
        }
        // Text fields count UTF-16 units; room for 10 code points of two units each, the rule above decides.
        public const int MaxNicknameInput = MaxNickname * 2;
        // A surrogate pair (an emoji) is one character, as on the server.
        public static int CodePoints(string text)
        {
            int count = 0;
            for (int i = 0; i < (text?.Length ?? 0); i += char.IsSurrogatePair(text, i) ? 2 : 1) count++;
            return count;
        }
        static bool Forbidden(UnicodeCategory c) => c == UnicodeCategory.Control || c == UnicodeCategory.Format || c == UnicodeCategory.PrivateUse
            || c == UnicodeCategory.OtherNotAssigned || c == UnicodeCategory.Surrogate;
        // Profile screen values; an empty weight means the 65 kg default.
        public static AccountSettings SettingsOf(PlayerProfile value)
        {
            if (value == null) return new AccountSettings();
            double weight = double.TryParse(value.Weight, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double kg) ? kg : AccountSettings.DefaultWeight;
            return Sanitize(weight, (int)value.Style, value.CalorieGoal);
        }

        private void Load(AccountProfile account)
        {
            Nickname = string.IsNullOrWhiteSpace(account.name) ? GuestName : account.name.Trim();
            settings = Sanitize(account.weight, account.style, account.goal);
            TutorialDone = account.tutorial_done;
            Monkeys = Math.Max(0, account.coins); traits.Restore(account.unlocked);
        }
        private static AccountSettings Sanitize(double weight, int style, double goal) => new AccountSettings
        {
            Weight = WorkoutEstimate.ValidWeight(weight) ? weight : AccountSettings.DefaultWeight,
            Style = style >= 0 && style <= 2 ? (PlayStyle)style : PlayStyle.FullBody,
            Goal = double.IsNaN(goal) || goal < 1 || goal > 5000 ? AccountSettings.DefaultGoal : goal,
        };
        private void Notify() { profile = null; Changed?.Invoke(); }
    }
}
