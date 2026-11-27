using UnityEngine;

namespace Mongscape.Presentation
{
    public enum LoginAction { None, Face, SignUp, Guest, Settings, Quit, Credits }

    // First screen after the title: face login, sign-up or guest play. Without the account server only guest play is offered.
    // Settings and quitting the game stay available here, so the game can be closed without signing in.
    internal sealed class LoginGateScreen
    {
        static readonly string[] Labels = { "얼굴 로그인", "회원가입", "게스트 플레이" };
        static readonly LoginAction[] Actions = { LoginAction.Face, LoginAction.SignUp, LoginAction.Guest };
        static readonly KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        static readonly KeyCode[] Keypad = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3 };

        readonly OverlaySkin skin;
        GUIStyle wrap;
        bool askQuit;
        public LoginGateScreen(OverlaySkin overlaySkin) { skin = overlaySkin; }

        public LoginAction Draw(IIdentityData identity, Texture2D logo)
        {
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.6f));
            GUI.matrix = oldMatrix;

            if (logo != null) GUI.DrawTexture(new Rect(40, 65, 485, 310), logo, ScaleMode.ScaleToFit, true);
            else skin.OutlinedText(new Rect(76, 170, 650, 115), "Mongscape", "Mongscape", 74, Palette.Cream, Palette.Outline, 2.5f, TextAnchor.UpperLeft);

            bool checking = identity == null || identity.ServerChecking;
            string problem = identity?.ServerProblem ?? "";
            bool server = !checking && string.IsNullOrEmpty(problem);
            skin.Text(new Rect(760, 40, 440, 44), "어떻게 시작할까요?", 34, Palette.Cream, TextAnchor.MiddleLeft);
            // An expired session's notice takes the place of the member line.
            string notice = identity?.LoginNotice ?? "";
            if (notice.Length > 0) skin.Text(new Rect(760, 90, 440, 60), notice, 20, Palette.Warn, TextAnchor.MiddleLeft, false, true);
            else skin.Text(new Rect(760, 90, 440, 60), "회원은 구한 원숭이로\n특성을 열고 기록이 남아요.", 22, Palette.Sand, TextAnchor.MiddleLeft, false, true);

            var action = LoginAction.None;
            var e = Event.current;
            for (int i = 0; i < Labels.Length; i++)
            {
                var r = new Rect(760, 160 + i * 128, 440, 80);
                bool guest = Actions[i] == LoginAction.Guest, enabled = guest || server;
                bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && enabled;
                Color text = !enabled ? Palette.A(Palette.Sand, 0.5f) : i == 0 ? Palette.Outline : Palette.Cream;
                bool pressed = false;
                // Without the server these are plain frames, not dimmed buttons, so the labels stay readable.
                if (enabled) pressed = skin.Button(r, Labels[i], i == 0 ? Palette.Gold : Palette.A(Palette.Ground, 0.92f), i == 0 ? Color.clear : Palette.Wood, text, i == 0 ? 30 : 26);
                else
                {
                    GUI.enabled = wasEnabled;
                    skin.BorderBox(r, Palette.A(Palette.Ground, 0.6f), Palette.WoodDim, 2);
                    skin.Text(r, Labels[i], 26, text, TextAnchor.MiddleCenter);
                    GUI.enabled = false;
                }
                skin.Text(new Rect(r.x + 14, r.y + 6, 30, 24), (i + 1).ToString(), 20, text, TextAnchor.UpperLeft, false, true);
                // Keys 1-3 choose like the buttons.
                if (GUI.enabled && e.type == EventType.KeyDown && (e.keyCode == Keys[i] || e.keyCode == Keypad[i])) { e.Use(); pressed = true; }
                GUI.enabled = wasEnabled;
                if (pressed) action = Actions[i];
            }

            // Only when the account server is down: what still works. Nothing is said while it is fine.
            wrap ??= new GUIStyle(skin.Style(20, false, TextAnchor.UpperLeft, true)) { wordWrap = true, clipping = TextClipping.Clip };
            if (!checking && !server) { GUI.color = Palette.Warn; GUI.Label(new Rect(760, 500, 440, 66), "지금은 얼굴 로그인과 회원가입을 쓸 수 없어요.\n게스트로 바로 플레이할 수 있어요.", wrap); GUI.color = Color.white; }
            var back = Palette.A(Palette.Ground, 0.9f);
            if (skin.Button(new Rect(504, 584, 200, 80), "크레딧", back, Palette.Wood, Palette.Cream, 26)) action = LoginAction.Credits;
            if (skin.Button(new Rect(752, 584, 200, 80), "게임 종료", back, Palette.Wood, Palette.Cream, 26)) askQuit = true;
            if (skin.Button(new Rect(1000, 584, 200, 80), "설정", back, Palette.Wood, Palette.Cream, 26)) action = LoginAction.Settings;
            skin.Text(new Rect(860, 678, 340, 30), BuildInfo.Label, 14, Palette.Muted, TextAnchor.MiddleRight, false);
            if (askQuit)
            {
                GUI.matrix = Matrix4x4.identity; skin.Fill(new Rect(0, 0, Screen.width, Screen.height), Palette.A(Palette.Ground, 0.8f)); GUI.matrix = oldMatrix;
                skin.BorderBox(new Rect(340, 220, 600, 280), Palette.A(Palette.Panel, 0.98f), Palette.Wood, 3);
                skin.Text(new Rect(340, 250, 600, 60), "게임을 끝낼까요?", 40, Palette.Gold, TextAnchor.MiddleCenter);
                if (skin.Button(new Rect(380, 360, 240, 80), "끝내기", back, Palette.Wood, Palette.Cream, 30)) { askQuit = false; action = LoginAction.Quit; }
                if (skin.Button(new Rect(660, 360, 240, 80), "취소", Palette.Gold, Color.clear, Palette.Outline, 30)) askQuit = false;
            }
            return action;
        }
    }
}
