using System;
using UnityEngine;

/// <summary>Crouch intent, safe capsule resizing, and a shared blend for animation/camera presentation.</summary>
[DefaultExecutionOrder(-50), RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
public sealed class PlayerStanceController : MonoBehaviour
{
    [SerializeField] private KeyCode crouchKey=KeyCode.C;
    [SerializeField, Min(.7f)] private float crouchedHeight=1.55f;
    [SerializeField, Min(.1f)] private float crouchedSpeed=1.05f;
    [SerializeField, Min(.1f)] private float crouchedAimSpeed=.85f;
    [SerializeField, Min(.05f)] private float blendTime=.18f;
    [SerializeField] private LayerMask obstructionLayers=1<<8;
    private CharacterController capsule;
    private ThirdPersonPlayerController movement;
    private PlayerHealth health;
    private float standingHeight,bottom,blendVelocity;
    private Vector3 standingCenter;
    private readonly Collider[] overlaps=new Collider[24];
    public bool IsCrouching {get;private set;}
    public float Blend {get;private set;}
    public float CrouchedSpeed=>crouchedSpeed;
    public float CrouchedAimSpeed=>crouchedAimSpeed;
    public event Action<bool> StanceChanged;

    private void Awake()
    {
        capsule=GetComponent<CharacterController>();movement=GetComponent<ThirdPersonPlayerController>();health=GetComponent<PlayerHealth>();
        standingHeight=capsule.height;standingCenter=capsule.center;bottom=standingCenter.y-standingHeight*.5f;
        crouchedHeight=Mathf.Clamp(crouchedHeight,capsule.radius*2+.05f,standingHeight);
    }
    private void Update()
    {
        if(Time.deltaTime<=0||capsule==null||!capsule.enabled)return;
        bool alive=health==null||!health.IsDead;
        if(alive&&Application.isFocused&&Cursor.lockState==CursorLockMode.Locked&&Input.GetKeyDown(crouchKey))TrySetCrouching(!IsCrouching);
        // A moving obstacle can enter the standing volume during the blend.
        if(!IsCrouching&&Blend>.001f&&!CanStand()){IsCrouching=true;StanceChanged?.Invoke(true);}
        Blend=Mathf.SmoothDamp(Blend,IsCrouching?1:0,ref blendVelocity,blendTime);
        float height=Mathf.Lerp(standingHeight,crouchedHeight,Blend);
        capsule.height=height;capsule.center=new Vector3(standingCenter.x,bottom+height*.5f,standingCenter.z);
    }
    public bool TrySetCrouching(bool value)
    {
        if(!isActiveAndEnabled||Time.timeScale<=0||capsule==null||(health!=null&&health.IsDead))return false;
        if(value==IsCrouching)return true;
        if(value&&movement!=null&&!movement.IsGrounded)return false;
        if(!value&&!CanStand())return false;
        IsCrouching=value;StanceChanged?.Invoke(value);return true;
    }
    public bool CanStand()
    {
        if(capsule==null)return false;
        float radius=Mathf.Max(.01f,capsule.radius-.015f);
        Vector3 lower=transform.TransformPoint(new Vector3(standingCenter.x,bottom+radius+.025f,standingCenter.z));
        Vector3 upper=transform.TransformPoint(new Vector3(standingCenter.x,bottom+standingHeight-radius,standingCenter.z));
        int count=Physics.OverlapCapsuleNonAlloc(lower,upper,radius,overlaps,obstructionLayers,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)return false;
        for(int i=0;i<count;i++)if(overlaps[i]!=capsule&&!overlaps[i].transform.IsChildOf(transform))return false;
        return true;
    }
    // Keep a safe crouched capsule if this component is temporarily disabled under a ceiling.
    private void OnDisable(){blendVelocity=0;}
}
