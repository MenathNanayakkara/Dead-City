using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Bounded Play Mode integration check on the enhanced copy. Does not save runtime changes.</summary>
[InitializeOnLoad]
public static class ExistingCitySmokeCheck
{
    private const string Key = "ReferenceCity.Smoke";
    private static int stage;
    private static double deadline;
    private static float started;
    private static readonly List<EnemyActor> actors = new List<EnemyActor>();
    private static readonly List<Vector3> positions = new List<Vector3>();
    private static readonly List<string> results = new List<string>();
    static ExistingCitySmokeCheck()
    {
        if(SessionState.GetBool(Key,false))Hook();
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ExistingCityEnhancer.ScenePath)throw new InvalidOperationException("Open the enhanced city copy.");
        SessionState.SetBool(Key,true);Hook();EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
    }
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        ExistingCityEnhancer.ConfigureStaticReflections(GameObject.Find("OutbreakDistrict").transform);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Begin();
    }
    private static void Hook(){stage=0;actors.Clear();positions.Clear();results.Clear();deadline=EditorApplication.timeSinceStartup+45;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    private static void Require(bool ok,string text){if(!ok)throw new Exception(text);results.Add("PASS: "+text);}
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Integration check timed out.");
            if(!EditorApplication.isPlaying)return;
            EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
            var ui=Object.FindAnyObjectByType<GameUIController>();
            if(ui!=null){OutbreakEditor.Values(ui,s=>s.FindProperty("pauseOnFocusLoss").boolValue=false);ui.Resume();}
            Time.timeScale=1;
            if(Time.time<.1f)return;
            if(stage==0)
            {
                var world=Object.FindAnyObjectByType<EnemyWorld>();var motor=world.movement;
                Require(motor!=null&&motor.enabled&&motor.IsGrounded,"Existing movement initializes and ground probe detects the street");
                var waves=Object.FindAnyObjectByType<WaveManager>();if(waves!=null)waves.enabled=false;
                foreach(var existing in world.Living.ToArray()){existing.Movement.Stop();StopBrains(existing);}
                var cc=motor.GetComponent<CharacterController>();Vector3 before=motor.transform.position;
                cc.Move(Vector3.forward);Physics.SyncTransforms();
                Require((motor.transform.position-before).z>.85f,"Player collision controller can move along the main street");
                Require(!Physics.CheckBox(new Vector3(0,1,-22),new Vector3(.45f,.8f,.45f),Quaternion.identity,1<<8,QueryTriggerInteraction.Ignore),"Central route stays clear of the added vehicle obstacles");
                var weapons=world.weapons;var aim=motor.GetComponent<PlayerAimController>();
                Require(weapons!=null&&weapons.CurrentWeapon!=null&&weapons.ActiveView!=null,"Existing weapon and muzzle references initialize");
                var camera=Camera.main;var rig=camera.GetComponent<ThirdPersonCameraController>();
                Require(rig!=null&&rig.enabled&&rig.PlanarForward.sqrMagnitude>.9f,"Existing camera orbit controller initializes");
                rig.enabled=false;camera.transform.position=motor.transform.position+new Vector3(.3f,1.6f,-3);
                var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Temporary smoke-test target";target.layer=8;
                target.transform.position=motor.transform.position+new Vector3(0,1.4f,10);target.transform.localScale=Vector3.one*1.8f;
                var receiver=target.AddComponent<ShootingTarget>();camera.transform.LookAt(target.transform.position);Physics.SyncTransforms();
                aim.SetAiming(true);int ammo=weapons.MagazineAmmo;float hp=receiver.CurrentHealth;
                Require(weapons.TryFire()&&weapons.MagazineAmmo==ammo-1,"Aimed shooting consumes one round");
                Require(receiver.CurrentHealth<hp&&!weapons.LastShotBlocked,"Camera-to-muzzle shooting hits and damages its target");
                Require(weapons.TryReload()&&weapons.IsReloading,"Reload begins using the existing weapon system");
                weapons.CancelReload();Require(weapons.SelectWeapon(1),"Existing weapon switching works");
                Object.Destroy(target);rig.enabled=true;aim.SetAiming(false);
                int i=0;foreach(string kind in new[]{"Hunter","Runner","Ambusher","Elite"})
                {
                    Require(NavMesh.SamplePosition(new Vector3(-.6f+i*.4f,0,-30),out var point,2,NavMesh.AllAreas),kind+" test start lies on navigation");
                    var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/"+kind+".prefab"),point.position,Quaternion.identity).GetComponent<EnemyActor>();
                    actor.Initialize(world);StopBrains(actor);
                    Require(actor.Movement.MoveTo(motor.transform.position,kind=="Runner"?4.8f:2.25f),kind+" accepts a route through the enhanced street");
                    actors.Add(actor);positions.Add(actor.transform.position);i++;
                }
                started=Time.time;stage=1;
            }
            else if(stage==1&&Time.time-started>2.5f)
            {
                for(int i=0;i<actors.Count;i++)
                {
                    var agent=actors[i].GetComponent<NavMeshAgent>();
                    Require(agent.isOnNavMesh&&agent.pathStatus==NavMeshPathStatus.PathComplete&&(actors[i].transform.position-positions[i]).magnitude>1,"Enemy "+i+" moves on a complete path around street obstacles");
                }
                results.Add("Scope: API/physics/agent integration checks. Keyboard, mouse feel, full waves and target-machine performance need manual review.");
                Finish();
            }
        }
        catch(Exception e){results.Add("FAIL: "+e);Finish();}
    }
    private static void StopBrains(EnemyActor actor)
    {
        foreach(var b in actor.GetComponents<MonoBehaviour>())if(b is HunterBrain||b is RunnerBrain||b is AmbusherBrain||b is EliteBrain)b.enabled=false;
    }
    private static void Finish()
    {
        File.WriteAllLines("Logs/ReferenceCitySmoke.txt",results);SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(results.Any(r=>r.StartsWith("FAIL:"))?1:0);
    }
}
