using System.Collections;
using Mongscape.Bootstrap;
using Mongscape.Domain;
using Mongscape.Infrastructure;
using Mongscape.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mongscape.Tests
{
    public sealed class GameSceneTests
    {
        private float oldBestDistance;
        private int oldBestScore;
        [SetUp] public void PreserveRecords() { oldBestDistance = PlayerPrefs.GetFloat("Muhanok.BestDistance", 0); oldBestScore = PlayerPrefs.GetInt("Muhanok.BestScore", 0); }
        [TearDown] public void RestoreRecords() { PlayerPrefs.SetFloat("Muhanok.BestDistance", oldBestDistance); PlayerPrefs.SetInt("Muhanok.BestScore", oldBestScore); PlayerPrefs.Save(); }
        [UnityTest] public IEnumerator TitleClickAndKeyboardUseRealInputRouter()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game = Object.FindFirstObjectByType<GameBootstrap>();
            var background = InputSystem.settings.backgroundBehavior;
            var editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>(); var keys = InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent(); keys.MakeCurrent(); InputSystem.EnableDevice(mouse); InputSystem.EnableDevice(keys);
            try
            {
                InputSystem.Update();
                InputState.Change(mouse, new MouseState().WithButton(MouseButton.Left)); game.TickInput();
                Assert.IsTrue(game.MenuOpen, "Clicking the title opens the login gate"); Assert.AreEqual(Mongscape.Presentation.MenuPage.Login, game.GetComponent<Mongscape.Presentation.HUDPresenter>().Page); Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);
                InputState.Change(mouse, new MouseState());
                ((Mongscape.Presentation.IHudData)game).PreviewStyle(PlayStyle.Keyboard);// a body style would wait for its calibration
                game.Session.Config.ExtendedRoutes=false;game.BeginRun(""); yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
                yield return new WaitUntil(()=>!game.IsRevealing);
                // Layout seed 2 opens with the duck row, which the crouch below passes while the run keeps going.
                game.Map.Reset(2);
                Assert.AreEqual(0,game.Intro.Elapsed,"Loading image is the intro; a second timeline must not run");
                InputState.Change(keys, new KeyboardState(Key.A)); game.TickInput();
                Assert.AreEqual(-1, game.Session.Runner.Lane);
                InputState.Change(keys, new KeyboardState(Key.S, Key.W)); game.TickInput();
                Assert.IsTrue(game.Session.Runner.Crouching && game.Session.Runner.KneeStepping);
                InputState.Change(keys, new KeyboardState()); yield return new WaitForSeconds(1.1f);
                Assert.IsFalse(game.Session.Runner.Crouching || game.Session.Runner.KneeStepping);
                InputState.Change(keys, new KeyboardState(Key.Space)); game.TickInput(); game.Session.Tick(.1f);
                Assert.Greater(game.Session.Runner.Y, 0);
                game.Session.Runner.SetStartZ(36);
                InputState.Change(keys,new KeyboardState(Key.A));game.TickInput();Assert.AreEqual(-1,game.Session.Runner.Lane);
                InputState.Change(keys,new KeyboardState(Key.C));game.TickInput();Assert.AreEqual(0,game.Session.Runner.Lane,"C must allow the middle hidden route from a side rail");
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keys); InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior; }
        }
        [UnityTest] public IEnumerator FullFlowPoolRetryAndFirstPerson()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game = Object.FindFirstObjectByType<GameBootstrap>(); Assert.IsNotNull(game);
            Assert.AreEqual(GameFlowState.TitleIdle, game.Session.Flow.State);
            Assert.IsNotNull(game.IntroTimeline); game.StartIntro();
            for (int i = 0; i < 82; i++) game.Intro.Tick(.1f);
            Assert.AreEqual(GameFlowState.Running, game.Session.Flow.State);
            Assert.Less(Vector3.Distance(game.CameraRig.FirstPerson.transform.position, game.Hero.Head), .001f);
            Assert.AreEqual(80, game.CameraRig.FirstPerson.Lens.FieldOfView);
            game.Session.Runner.Command(MotionCommand.Left);
            game.Session.Tick(.2f); Assert.AreEqual(-2.2f, game.Session.Runner.X, .0001f);
            game.Session.Runner.Command(MotionCommand.Jump); game.Session.Tick(.1f); Assert.Greater(game.Session.Runner.Y, .5f);
            for (int i = 0; i < 20; i++) game.Session.Tick(.1f);
            game.Session.Runner.SetStartZ(80);game.Session.Runner.Command(MotionCommand.Crouch); game.Session.Runner.Command(MotionCommand.KneeStep);
            Assert.IsTrue(game.Session.Runner.Crouching && game.Session.Runner.KneeStepping);
            for (int i = 0; i < 11; i++) game.Session.Tick(.1f); Assert.IsFalse(game.Session.Runner.Crouching || game.Session.Runner.KneeStepping);
            int created = game.Map.CreatedCount;
            for (int n = 0; n < 120; n++)
            {
                game.Map.Tick(n * 24f + 2);
                var active = game.Map.Active;
                Assert.GreaterOrEqual(active.Count, 4);
                for (int i = 0; i < active.Count; i++)
                {
                    var chunk = active[i].Chunk; Assert.AreEqual(24, chunk.Exit.position.z - chunk.Entry.position.z, .001f);
                    if (i > 0) Assert.Less(Vector3.Distance(active[i - 1].Chunk.Exit.position,chunk.Entry.position),.001f);
                    Assert.AreEqual(RouteSurface.BaseHeight(chunk.Sequence),chunk.Entry.position.y,.001f);
                    for (int row = 0; row < 2; row++)
                    {
                        int count = 0; for (int lane = 0; lane < 3; lane++) if (chunk.Obstacles[row * 3 + lane].gameObject.activeSelf) count++;
                        Assert.IsTrue(count<=1||count==3&&SafePatternValidator.IsActionRowSafe(7,chunk.Obstacles[row*3].Kind));
                    }
                }
                Assert.AreEqual(created, game.Map.CreatedCount);
            }
            for (int i = 0; i < 2; i++) { game.Session.Tick(.7f); game.Session.Hit(true); }
            game.Session.Tick(1.3f);
            Assert.AreEqual(GameFlowState.Caught, game.Session.Flow.State);
            yield return new WaitForSecondsRealtime(4.1f); Assert.AreEqual(GameFlowState.Result, game.Session.Flow.State);
            game.Retry(); Assert.AreEqual(GameFlowState.TitleIdle, game.Session.Flow.State);
            Assert.AreEqual(0, game.Session.Score.Score); Assert.AreEqual(0, game.Session.Chase.Pressure); Assert.AreEqual(2, game.Session.Runner.Z);
            Assert.AreEqual(created, game.Map.CreatedCount); Assert.AreEqual(0, game.Map.Active[0].Chunk.Entry.position.z);
            game.StartIntro(); for (int i = 0; i < 82; i++) game.Intro.Tick(.1f);
            Assert.AreEqual(GameFlowState.Running, game.Session.Flow.State);
        }
        [UnityTest] public IEnumerator ItemAurasShowWhileActiveAndShieldBreaksOnHit()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            game.StartIntro(); for(int i=0;i<82;i++) game.Intro.Tick(.1f);
            game.enabled=false;
            var aura=game.GetComponent<Mongscape.Presentation.PowerUpAuraView>(); Assert.IsNotNull(aura);
            yield return null; Assert.IsFalse(aura.ShieldVisible||aura.CallVisible);
            game.Session.Items.Activate(ItemKind.Shield); game.Session.Items.Activate(ItemKind.Call); yield return null;
            Assert.IsTrue(aura.ShieldVisible,"Shield bubble"); Assert.IsTrue(aura.CallVisible,"Call sparks");
            Assert.IsFalse(game.Session.Hit(true)); Assert.IsFalse(game.Session.Items.IsActive(ItemKind.Shield)); yield return null;
            Assert.IsTrue(aura.ShieldVisible,"The used shield bursts visibly before disappearing");
            yield return new WaitForSecondsRealtime(Mongscape.Presentation.PowerUpAuraView.BurstSeconds+.3f);
            Assert.IsFalse(aura.ShieldVisible); Assert.IsTrue(aura.CallVisible);
        }
        [UnityTest] public IEnumerator ChunksCarryNoCoins()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            game.StartIntro(); for(int i=0;i<82;i++) game.Intro.Tick(.1f);
            game.enabled=false;
            foreach(var active in game.Map.Active) Assert.IsNull(active.Chunk.transform.Find("Collectibles"),"Plan 4: no coins on the course");
        }
        [UnityTest] public IEnumerator OnlyUnlockedItemsSpawnAwayFromBananaRows()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            try
            {
                game.StartIntro(); for(int i=0;i<82;i++) game.Intro.Tick(.1f);
                game.enabled=false;
                foreach(var allowed in new[]{new ItemKind[0],new[]{ItemKind.Shield}})
                {
                    // Items come from the member's unlocks: a synthetic member in memory, no server calls.
                    var ids=allowed.Length==0?new int[0]:new[]{11};
                    game.Account.SignInMember(new Mongscape.Application.AccountProfile{id="items-qa",name="아이템",unlocked=ids},"");
                    game.Map.Reset(); int found=0;
                    for(int n=0;n<80;n++)
                    {
                        game.Map.Tick(n*24f+2);
                        foreach(var active in game.Map.Active) foreach(var pickup in active.Chunk.Pickups)
                        {
                            found++; CollectionAssert.Contains(allowed,pickup.Kind);
                            float z=pickup.transform.position.z;
                            foreach(var peel in active.Chunk.DynamicFloorItems) Assert.GreaterOrEqual(Mathf.Abs(peel.transform.position.z-z),2.99f,"Items stay clear of banana rows");
                        }
                    }
                    if(allowed.Length==0) Assert.AreEqual(0,found,"Nothing unlocked: no items appear");
                    else Assert.Greater(found,0,"Unlocked items appear on the map");
                }
            }
            finally { game.Account.SignOut(); }
        }
        [UnityTest] public IEnumerator RunnerFollowsCurvesAndStairElevation()
        {
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>(); game.StartIntro();
            for(int i=0;i<82;i++) game.Intro.Tick(.1f);
            game.enabled=false;
            foreach(int sequence in new[]{1,4,5,8})
            {
                int type=RouteSurface.TypeAt(sequence);
                for(float z=game.Map.Active[0].Chunk.Entry.position.z;z<=sequence*24;z+=12) game.Map.Tick(z+2);
                MongscapeMapChunk chunk=null;
                foreach(var active in game.Map.Active) if(active.Type==type) { chunk=active.Chunk; break; }
                Assert.IsNotNull(chunk); Vector3 position=new Vector3(0,0,chunk.Entry.position.z+11f);
                RouteSurface.Sample(position.z,out float offsetX,out float offsetY); position.x=offsetX;
                game.Session.Runner.Tick(.3f,0); game.Session.Runner.SetStartZ(position.z);
                game.Hero.Apply(game.Session.Runner);
                Assert.AreEqual(offsetY+(game.Session.Runner.Riding?.25f:0),game.Hero.transform.position.y,.001f);
                Assert.AreEqual(position.x+game.Session.Runner.WorldLaneOffset,game.Hero.transform.position.x,.001f);
            }
        }
        [UnityTest] public IEnumerator MissingCharacterAssetsUseFallback()
        {
            // Disable this scene's composition before loading a copy with empty art references.
            yield return SceneManager.LoadSceneAsync("GameScene"); yield return null;
            var template = Object.FindFirstObjectByType<GameBootstrap>();
            var copy = new GameObject("Fallback Test"); copy.SetActive(false);
            var game = copy.AddComponent<GameBootstrap>(); game.Settings = template.Settings; game.ChunkPrefabs = template.ChunkPrefabs;
            game.CameraRig = template.CameraRig; game.OutputCamera = template.OutputCamera; game.IntroTimeline = template.IntroTimeline;
            Object.Destroy(template.gameObject); yield return null;
            copy.SetActive(true); yield return null;
            Assert.AreEqual(GameFlowState.TitleIdle, game.Session.Flow.State); Assert.IsNotNull(game.Hero);
            game.StartIntro(); for (int i = 0; i < 82; i++) game.Intro.Tick(.1f);
            Assert.AreEqual(GameFlowState.Running, game.Session.Flow.State);
            Object.Destroy(copy); yield return null;
        }
    }
}
