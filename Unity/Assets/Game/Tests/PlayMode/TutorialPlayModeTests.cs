using System;
using System.Collections;
using System.IO;
using Mongscape.Application;
using Mongscape.Bootstrap;
using Mongscape.Domain;
using Mongscape.Infrastructure;
using Mongscape.Presentation;
using Mongscape.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Mongscape.Tests
{
    // The tutorial runs in GameScene on its fixed course; a member with unlocked items plays it against a fake account server.
    public sealed class TutorialPlayModeTests
    {
        private string root;
        private GameBootstrap game;
        private HUDPresenter hud;
        private FakeAccountGateway fake;
        [SetUp] public void TempFolder() { root=Path.Combine(Path.GetTempPath(),"mongscape-tutorial-"+Guid.NewGuid().ToString("N")); }
        [TearDown] public void RemoveFolder() { Time.timeScale=1; if(Directory.Exists(root))Directory.Delete(root,true); }

        private IEnumerator OpenAsMember()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            game=Object.FindFirstObjectByType<GameBootstrap>();hud=game.GetComponent<HUDPresenter>();fake=new FakeAccountGateway();
            game.UseAccountServices(fake,new PlayerProgressStore(root));
            fake.Profile.coins=40;fake.Profile.unlocked=new[]{11,41};
            game.Account.SignInMember(fake.Profile,"fake-token");Assert.IsTrue(game.SignedIn);Assert.IsFalse(game.Account.TutorialDone);
            ((IHudData)game).PreviewStyle(PlayStyle.Keyboard);
        }
        private IEnumerator WaitRunning()
        {
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.IsRevealing||game.Session.Flow.State!=GameFlowState.Running)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);Assert.IsTrue(game.Session.IsTutorial);Assert.IsTrue(game.Tutorial.Active);
        }

        [UnityTest]
        public IEnumerator TutorialMistakeRetriesAndLeavesRankingMonkeysAndItemsAlone()
        {
            yield return OpenAsMember();
            game.OpenMainMenu();hud.SelectMenu(MenuAction.Tutorial);
            Assert.AreEqual(MenuPage.TutorialOffer,hud.Page,"The menu's 튜토리얼 asks for the play style first");
            hud.ChooseTutorial(TutorialChoice.Start,PlayStyle.Keyboard,TutorialChapter.Basics);
            yield return WaitRunning();
            Assert.AreEqual(PlayStyle.Keyboard,game.Tutorial.Script.Style);
            CollectionAssert.IsEmpty(game.RunItems(),"No item spawns in the tutorial");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(0,Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(TutorialStep.Lane,game.Tutorial.Script.Current);
            Assert.IsTrue(game.Session.Hit(true));
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State,"A tutorial mistake is never a capture");
            yield return null;yield return null;
            Assert.IsTrue(game.Tutorial.Retrying);Assert.AreEqual("다시 하기",game.Tutorial.Message);
            float deadline=Time.realtimeSinceStartup+5;
            while(game.Tutorial.Retrying&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(game.Tutorial.Retrying);Assert.AreEqual(1,game.Tutorial.Script.TotalRetries);Assert.AreEqual(TutorialStep.Lane,game.Tutorial.Script.Current);
            Assert.Less(game.Session.Runner.Z,TutorialCourse.LaneChunk*RouteSurface.Length+TutorialCourse.LaneBlockZ,"Restarted in front of the step");
            game.TogglePause();game.ExitToMenu();
            Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);Assert.IsFalse(game.Session.IsTutorial);Assert.AreEqual(MenuPage.Main,hud.Page);
            Assert.AreEqual(0,game.Store.Board.All.Count,"Nothing goes to this PC's ranking");
            Assert.AreEqual(0,fake.SavedRuns.Count,"Nothing goes to the account server");
            Assert.AreEqual(40,game.Account.Monkeys);Assert.IsFalse(game.Account.TutorialDone);CollectionAssert.DoesNotContain(fake.Calls,"profile/update");
            Assert.IsNotEmpty(game.RunItems(),"Ordinary runs keep the unlocked items");
            game.FaceLogout(false);
        }

        // The prison chapter starts with its stair lesson, in front of the stair chunk.
        private IEnumerator DriveToStairs()
        {
            yield return OpenAsMember();
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Tutorial);hud.ChooseTutorial(TutorialChoice.Start,PlayStyle.Keyboard,TutorialChapter.Prison);
            yield return WaitRunning();
            Assert.AreEqual(TutorialStep.Stair,game.Tutorial.Script.Current,"The prison chapter begins with the stair");
            Assert.AreEqual(7,RouteSurface.TypeAt(TutorialCourse.PrisonStairChunk));
            Time.timeScale=3;
            float deadline=Time.realtimeSinceStartup+40;
            while(game.Tutorial.LessonDistance>TutorialDirector.ShowDistance&&Time.realtimeSinceStartup<deadline)yield return null;
        }

        [UnityTest]
        public IEnumerator MenuTutorialPicksABodyStyleThatCalibratesFirstAndBackReturnsToTheMenu()
        {
            yield return OpenAsMember();
            game.OpenMainMenu();hud.SelectMenu(MenuAction.Tutorial);
            hud.ChooseTutorial(TutorialChoice.Skip);Assert.AreEqual(MenuPage.Main,hud.Page,"돌아가기 from the menu's tutorial choice");
            game.OpenMainMenu();hud.SelectMenu(MenuAction.Tutorial);hud.ChooseTutorial(TutorialChoice.Start,PlayStyle.UpperBody,TutorialChapter.Basics);
            float deadline=Time.realtimeSinceStartup+10;
            while((game.IsLoading||game.Session.Flow.State!=GameFlowState.Running)&&Time.realtimeSinceStartup<deadline)yield return null;
            // --mongscape-no-camera has no webcam to play a body style with: the keyboard is chosen instead.
            var style=CameraAccess.Disabled?PlayStyle.Keyboard:PlayStyle.UpperBody;
            Assert.AreEqual(style,game.Tutorial.Script.Style);
            CollectionAssert.AreEqual(new TutorialScript(TutorialChapter.Basics,style).Steps,game.Tutorial.Script.Steps);
            if(!CameraAccess.Disabled){Assert.AreEqual(TutorialStep.Camera,game.Tutorial.Script.Steps[0],"Body styles check the camera first");Assert.AreEqual(TutorialStep.Calibrate,game.Tutorial.Script.Steps[1],"then calibrate before the first lesson");}
            game.ExitToMenu();game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator LessonsRunAtFullSpeedAndAnEarlyJumpPassesAtOnce()
        {
            yield return OpenAsMember();
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Play);hud.ChooseTutorial(TutorialChoice.Start);
            yield return WaitRunning();
            var run=game.Session.Runner;Time.timeScale=2;
            float deadline=Time.realtimeSinceStartup+60;
            bool earlyJumped=false;
            while(game.Tutorial.Active&&game.Tutorial.Script.Current!=TutorialStep.Crouch&&Time.realtimeSinceStartup<deadline)
            {
                var t=game.Tutorial;
                if(!t.Retrying)
                {
                    Assert.AreEqual(1f,game.Session.SpeedScale,"No slow motion in front of a lesson");
                    if(t.Script.Current==TutorialStep.Lane&&t.Waiting&&run.Lane==0)run.Command(MotionCommand.Right);
                    else if(t.Script.Current==TutorialStep.Jump&&t.Waiting&&!earlyJumped)
                    {
                        Assert.IsNull(t.Cue,"The jump call waits for its timing");
                        Assert.AreEqual("준비하기",t.Message);
                        // Far too early for the crate: the lesson still completes on the jump itself.
                        run.Command(MotionCommand.Jump);earlyJumped=true;
                    }
                }
                yield return null;
            }
            Assert.IsTrue(earlyJumped);Assert.AreEqual(TutorialStep.Crouch,game.Tutorial.Script.Current,"The jump completed the lesson");
            // Landing in front of the crate is forgiven: running past it does not restart the jump lesson.
            float end=(TutorialCourse.JumpChunk+1)*RouteSurface.Length;
            while(run.Z<end&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(game.Tutorial.Retrying);Assert.AreEqual(0,game.Tutorial.Script.TotalRetries);
            Time.timeScale=1;game.ExitToMenu();game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator StairLessonWithoutStepsIsAMissAndRestarts()
        {
            yield return DriveToStairs();
            Time.timeScale=3;
            float deadline=Time.realtimeSinceStartup+40;
            while(!game.Tutorial.Retrying&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.Tutorial.Retrying,"Standing still on the steps is a counted miss");
            Assert.AreEqual(TutorialStep.Stair,game.Tutorial.Script.Current);Assert.AreEqual(1,game.Tutorial.Script.Retries);
            Assert.AreEqual(0,game.Session.SlipStrikes,"No real penalty in the tutorial");
            Time.timeScale=1;game.ExitToMenu();game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator StairChunkExposesCueGaugeAndCameraPitchThenReturnsToNormal()
        {
            yield return DriveToStairs();
            var run=game.Session.Runner;var model=hud.RunModel;var fx=game.StairFx;
            bool cue=false,gauge=false;float pitch=0,bob=0;
            float deadline=Time.realtimeSinceStartup+60,stairEnd=(TutorialCourse.PrisonStairChunk+1)*RouteSurface.Length;
            // The first knee step completes the lesson; the climb goes on to the top of the stair chunk.
            while(game.Tutorial.Active&&run.Z<stairEnd&&Time.realtimeSinceStartup<deadline)
            {
                bool atFoot=game.Tutorial.Script.Current==TutorialStep.Stair&&game.Tutorial.LessonDistance<=3.6f;
                if(!game.Tutorial.Retrying&&(atFoot||StairJudge.StepDue(run))&&run.SinceKneeStep>=.4f)run.Command(MotionCommand.KneeStep);
                cue|=ActionCuePresenter.Find(game.Session,game.Map,PlayStyle.Keyboard)=="W 연타!";
                gauge|=model.ShowStair&&model.KneeGauge>0f&&model.StairProgress>0f;
                pitch=Mathf.Max(pitch,fx.Pitch);bob=Mathf.Max(bob,fx.Bob);
                yield return null;
            }
            Assert.IsTrue(cue,"Keyboard play is told W 연타 before the stairs");
            Assert.IsTrue(gauge,"The knee gauge and progress are exposed while climbing");
            Assert.Greater(pitch,1f,"The camera tilts up on the stairs");Assert.Greater(bob,.0f,"A step bobs the camera");
            Time.timeScale=1;game.ExitToMenu();game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator PrisonChapterOpensTheRealCellWhenTheRunnerHidesThroughTheLight()
        {
            yield return DriveToStairs();
            var run=game.Session.Runner;Time.timeScale=3;
            float deadline=Time.realtimeSinceStartup+90;
            bool sawGauge=false;
            while(game.Tutorial.Active&&game.Tutorial.Script.Current!=TutorialStep.AlarmRule&&Time.realtimeSinceStartup<deadline)
            {
                var step=game.Tutorial.Script.Current;
                if(!game.Tutorial.Retrying)
                {
                    if(step==TutorialStep.Stair&&run.SinceKneeStep>=.4f)run.Command(MotionCommand.KneeStep);
                    else if(step==TutorialStep.Hide&&game.Session.Prison.Holding){run.Command(MotionCommand.Crouch);sawGauge|=game.Session.Prison.LockGauge>0f;}
                }
                yield return null;
            }
            Assert.AreEqual(TutorialStep.AlarmRule,game.Tutorial.Script.Current,"Hiding through the real searchlight opens the cell and passes the step");
            Assert.IsTrue(sawGauge,"The real lock gauge filled");Assert.IsTrue(game.Session.Prison.Opened[0]);
            Time.timeScale=1;game.ExitToMenu();game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator ZooChapterWaitsAtItsBeatStationOnTheZooMap()
        {
            yield return OpenAsMember();
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Tutorial);hud.ChooseTutorial(TutorialChoice.Start,PlayStyle.Keyboard,TutorialChapter.Zoo);
            yield return WaitRunning();
            Assert.IsTrue(RouteSurface.IsZoo(RouteSurface.TypeAt(0)),"The zoo chapter runs on the zoo map");
            Assert.AreEqual(TutorialStep.Verse,game.Tutorial.Script.Current,"Keyboard play has no sync adjustment");
            Time.timeScale=3;
            float deadline=Time.realtimeSinceStartup+30;
            while(!game.Tutorial.Waiting&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.Tutorial.Waiting);Assert.AreEqual("Space와 W 누르기",game.Tutorial.Message);
            // The verse is the real zoo: the song on the easy chart. Hitting its notes leads to the lion in the mirror.
            var zoo=game.GetComponent<Mongscape.Presentation.Map.ZooMapView>();
            deadline=Time.realtimeSinceStartup+40;
            while(!zoo.Chase.Active&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(zoo.Chase.Active&&zoo.Chase.Practice,"The zoo song plays");Assert.AreEqual(ZooPractice.Chart,zoo.Chase.Map.Chart);
            while(game.Tutorial.Active&&game.Tutorial.Script.Current==TutorialStep.Verse&&Time.realtimeSinceStartup<deadline)
            {
                var notes=zoo.Chase.Map.Notes;
                for(int i=0;i<notes.Count;i++)if(!zoo.Chase.IsJudged(i)&&Mathf.Abs(notes[i].Time-zoo.Chase.SongTime)<.2f)zoo.Chase.Input(notes[i].Kind);
                yield return null;
            }
            Assert.AreEqual(TutorialStep.Rearview,game.Tutorial.Script.Current,"The verse passes");
            yield return null;yield return null;
            Assert.IsTrue(zoo.Chase.MirrorVisible,"The lion shows in the mirror");
            deadline=Time.realtimeSinceStartup+30;
            while(game.Session.Flow.State==GameFlowState.Running&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreNotEqual(GameFlowState.Running,game.Session.Flow.State,"The chapter ends in the menu");Assert.IsFalse(zoo.Chase.Active);
            Time.timeScale=1;game.ExitToMenu();game.FaceLogout(false);
        }

        [UnityTest]
        public IEnumerator KeyboardTutorialCompletesAndMarksTheMember()
        {
            yield return OpenAsMember();
            hud.OpenMainMenu();hud.SelectMenu(MenuAction.Play);
            Assert.AreEqual(MenuPage.TutorialOffer,hud.Page,"An unfinished member is offered the tutorial on 게임 시작");
            hud.ChooseTutorial(TutorialChoice.Start);
            yield return WaitRunning();
            CollectionAssert.AreEqual(new TutorialScript(TutorialChapter.Basics,PlayStyle.Keyboard).Steps,game.Tutorial.Script.Steps);
            var run=game.Session.Runner;
            float deadline=Time.realtimeSinceStartup+200;
            while(game.Tutorial.Active&&Time.realtimeSinceStartup<deadline)
            {
                var t=game.Tutorial;var step=t.Script.Current;
                if(!game.Session.Paused)Time.timeScale=3;
                if(step==TutorialStep.Rescue&&t.Waiting)game.Session.Command(MotionCommand.ReachRight);
                // Esc pauses (the pause step) and the pause screen's resume counts back in.
                if(game.Session.Paused&&!game.PauseControl.Counting){game.TogglePause();yield return null;continue;}
                if(!t.Retrying&&!t.Script.Finished&&game.Session.Flow.State==GameFlowState.Running)
                {
                    bool near=t.LessonDistance<=TutorialDirector.ShowDistance;
                    if(step==TutorialStep.Lane&&near&&run.Lane==0)run.Command(MotionCommand.Left);
                    else if(step==TutorialStep.Jump&&t.Cue==ActionCuePresenter.JumpCue&&run.Y<=.001f)run.Command(MotionCommand.Jump);
                    else if(step==TutorialStep.Crouch&&t.Cue==ActionCuePresenter.CrouchCue)run.Command(MotionCommand.Crouch);
                    else if(step==TutorialStep.Banana&&near&&run.Lane==TutorialCourse.BlockedLane)run.Command(MotionCommand.Left);
                    else if(step==TutorialStep.PauseX&&t.Waiting&&!game.Session.Paused&&run.Z>=TutorialCourse.HoldZ(TutorialChapter.Basics,step)-.5f)game.TogglePause();
                    else if(step==TutorialStep.Fork&&(RouteSurface.ActivePlan?.ForkWindow(run.Z)??false)&&run.Lane==0)run.Command(MotionCommand.Left);
                }
                yield return null;
            }
            Time.timeScale=1;
            Assert.IsFalse(game.Tutorial.Active,"The keyboard tutorial reaches the end");
            Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);Assert.AreEqual(MenuPage.Main,hud.Page);
            // The frame that finished the tutorial stops there: no running dust or run fade over the menu.
            var dust=game.Hero.transform.Find("VFX_Dust_Run")?.GetComponentInChildren<ParticleSystem>();
            if(dust!=null)Assert.IsFalse(dust.isPlaying,"Running dust stays stopped on the menu");
            Assert.AreEqual(1f,hud.Fade);
            Assert.IsTrue(game.Account.TutorialDone);CollectionAssert.Contains(fake.Calls,"profile/update");
            Assert.IsTrue(fake.Updates[fake.Updates.Count-1].TutorialDone==true);
            Assert.AreEqual(0,game.Store.Board.All.Count);Assert.AreEqual(0,fake.SavedRuns.Count);Assert.AreEqual(40,game.Account.Monkeys);
            hud.SelectMenu(MenuAction.Play);Assert.AreEqual(MenuPage.Setup,hud.Page,"A finished member goes straight to the setup");
            game.FaceLogout(false);
        }
    }
}
