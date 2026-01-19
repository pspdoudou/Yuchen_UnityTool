using CharacterMovement;
using UnityEngine;
using FMODUnity;
using System.Collections;
using FMOD.Studio;
using Sirenix.OdinInspector;
using Unity.AI.Navigation;
using UnityEngine.AI;
using GameEvents;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Unity.VisualScripting;
using UnityEditor;

public class CustomCharacterMovement : CharacterMovement3D
{
    [field: FormerlySerializedAs("<MovementTolerance>k__BackingField")] [field: SerializeField, BoxGroup("Navigation")] public float ArriveCheckDistance { get; protected set; } = 1.5f;
    [ReadOnly] public bool IsLinkJumping = false;


    [SerializeField] private PlayerMovementData playerMovementData;
    public bool SlowMoGroundCheck => Physics.Raycast(transform.position + new Vector3(0,0.3f,0), new Vector3(0, -1, 0) , 1f, GroundMask);


    #region DoubleJump
    [SerializeField, BoxGroup("Jump Setting")] private int _totalJumpsInAir = 2;// number of jumps that allow player to do while in the air
    [SerializeField, BoxGroup("Jump Setting")] private float _jumpHeightIncreasePerJump = 0.5f;
    [SerializeField, BoxGroup("Jump Setting")] private EventReference _jumpEventReference;
    
    private float _postJumpGroundingTime = 0.08f;
    private int _lastJumpFixedTick = -1;
    private float _postJumpTimer = 0f;

    private int _jumpCounter = 0;
    private float _initialJumpHeight;
    public UnityEvent OnDoubleJump;
    private bool _requestJump = false;
    public bool IsJumping = false;
    
    bool groundedSafe => IsGrounded && _postJumpTimer <= 0f && Rigidbody.linearVelocity.y <= 0.01f;



    public void PerformJump()
    {
        if (CanCoyoteJump || _jumpCounter < _totalJumpsInAir)
        {
            if (CanCoyoteJump && !IsJumping)// if on the ground, reset jump counter and jump height
            {
                _jumpCounter = 0;
                JumpHeight = _initialJumpHeight;
            }
            else
            {
                OnDoubleJump.Invoke();
            }
            if (_jumpCounter < _totalJumpsInAir)
            { 
                Jump(); // jump
                PlayJumpSound();
                _jumpCounter++;
                IsJumping = true;
            }
            JumpHeight = JumpHeight + _jumpHeightIncreasePerJump; //increase the jump heiget after jump
        }
    }

    private void PlayJumpSound()
    {
        if (!_jumpEventReference.IsNull && !IsSliding)
        {
            EventInstance instance = RuntimeManager.CreateInstance(_jumpEventReference);
            instance.setParameterByName("JumpCount", (_jumpCounter == 0) ? 0 : 1);
            instance.setParameterByName("Speed", 1f);
            instance.start();
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
            instance.release();
        }
    }

    public override void TryJump()
    {
      _requestJump = true;
    }


    #endregion

    #region Dashing
    [Header("Dash Settings")]
    //[SerializeField, HideInInspector] private float _dashImpulse = 20f; //Dash force,  OBSOLETE!
    [SerializeField] private float _dashDistance = 6f;
    [SerializeField] private float _dashAccDuration = 0.15f;
    [SerializeField] private float _stoppingTime = 0.1f;
    [SerializeField] private float _dashCooldown = 1f; //Time before you can dash again (in seconds)
    [SerializeField] private FloatEventAsset _coolDownPrec;
    [SerializeField] private EventReference _dashSFX; //sfx
    [SerializeField] private EventReference _dashFailSFX; //sfx
    public Vector3 HorizontalVel => new Vector3(Rigidbody.linearVelocity.x, 0f, Rigidbody.linearVelocity.z);
    private float dashTotalTime => _dashAccDuration + _stoppingTime + _dashCooldown;
    public bool IsDashing {  get; protected set; } = false;
    private float _dashTimer;
    private Vector3 _preDashFlatVel;   // velocity before dash
    private float _savedLinearDamping;
    private bool _savedUseGravity;
    private float _dashSavedAcc;
    public UnityEvent OnDash;
    public void StartDash(Vector3 dashDir)
    {
        if (!IsDashing)
        {
            if (dashDir.sqrMagnitude <= 0.001f)
            {
                Vector3 camForward = Camera.main.transform.forward;
                if(camForward != null) dashDir = camForward;
            } 
            StartCoroutine(DashRoutine(dashDir));
            OnDash.Invoke();
        }
        else PlayDashFailSound();
    }
    private IEnumerator DashRoutine(Vector3 dir)
    {
        Acceleration = 0f;
        _dashTimer = 0f;
        IsDashing = true;        
        // cache initial setting
        _savedUseGravity = Rigidbody.useGravity;
        _savedLinearDamping = Rigidbody.linearDamping;
        _preDashFlatVel = HorizontalVel;
        dir.y = 0f;

        Vector3 velOnDashDir = dir * _preDashFlatVel.magnitude;
        dir = dir.normalized;
        float dashSpeed = _dashDistance / _dashAccDuration; //calculate the speed
        float dashPassIntensity;
        //disable all factor than can affect the dash
        Rigidbody.useGravity = false;  
        Rigidbody.linearDamping = 0f;

        Shader.SetGlobalFloat("_DashBaseIntensity", 0.5f); // Dash Vignette

        if (!_dashSFX.IsNull) RuntimeManager.PlayOneShot(_dashSFX, transform.position); // play dash SFX

        //Give speed, Do dash 
        Rigidbody.linearVelocity = dir * dashSpeed;
        float dashTime = 0f;
        bool isHitObstcale = false;
        while (dashTime < _dashAccDuration)
        {
            dashTime += Time.deltaTime;
            // if hit something, stop
            if (Physics.Raycast(transform.position + new Vector3(0,0.5f,0), dir, out RaycastHit hit, 0.6f, GroundMask))
            {
#if UNITY_EDITOR
                Debug.Log("dash hit obstacle");
#endif
                isHitObstcale = true;
                Shader.SetGlobalFloat("_DashBaseIntensity", 0);
                Rigidbody.linearVelocity = Vector3.zero;
                break;
            }
            yield return null;
        }

        //slow down to original speed after the dash is finished
        Vector3 dashVel = Rigidbody.linearVelocity;
        float stoppingTime = 0f;
        while(stoppingTime < _stoppingTime)
        {

            if(isHitObstcale) break;
            stoppingTime += Time.deltaTime;
            float smoothProgress = Mathf.SmoothStep(0f, 1f , stoppingTime / _stoppingTime);

            Rigidbody.linearVelocity = Vector3.Lerp(dashVel, velOnDashDir, smoothProgress); // slow down to og speed, but lerp it

            dashPassIntensity = Mathf.Lerp(0.5f, 0, smoothProgress);
            Shader.SetGlobalFloat("_DashBaseIntensity", dashPassIntensity);
            yield return null;
        }

        // restore the initial setting
         Rigidbody.useGravity = _savedUseGravity;
         Rigidbody.linearDamping = _savedLinearDamping;
         Acceleration = _dashSavedAcc;
       
    }


    private void UpdateDashCoolDown()
    {
        _dashTimer += Time.deltaTime;
        float coolDown = Mathf.Clamp01(_dashTimer / dashTotalTime);
        if (coolDown == 1)
        {
            _dashTimer = dashTotalTime;
            IsDashing = false;
        }

        _coolDownPrec.Invoke(coolDown);
       // Debug.Log(coolDown);
    
    }

    private void PlayDashFailSound()
    {
        if (!_dashFailSFX.IsNull) RuntimeManager.PlayOneShotAttached(_dashFailSFX, gameObject);
    }


    #endregion

    #region Sliding
    [Header("Slide Setting")]
    [SerializeField, Tooltip("The Acceleration Time")] private float _slideAcceleDuration = 0.2f;
    [SerializeField, Tooltip("The Slide Speed = this factor * current speed")] private float _slideSpeedMultiplier = 4f;
    [SerializeField, Tooltip("The Slide Height =  this factor * player scale")] private float _slideHeightMultiplier = 0.5f;
    //[SerializeField] private float _slideSpeedDampening = 0.9f;
    [SerializeField, Tooltip("The Speed will be reduced by this friction")] private float _slideFriction = 3f;
    [SerializeField, Tooltip("The Direction Control when player sliding")] private float _slideSteerDegPerSec = 50f;
    [SerializeField, Tooltip("When Slide Speed reaches this threshold, slide stops")] private float _stopSlideSpeedThreshold = 0.5f;
    [SerializeField, Tooltip("The Braking speed when player input is opposite to current speed ")] private float _slideBrakeStrength = 6f;
    [SerializeField] private FloatEventAsset _slideProgPrec;
    [SerializeField] private EventReference _slideEnterReference;
    [SerializeField] private EventReference _slideExitReference;
    [SerializeField] private StudioEventEmitter _slideEmitter;

    public float HorizontalSpeed => new Vector2(Rigidbody.linearVelocity.x, Rigidbody.linearVelocity.z).magnitude;
    public bool IsSliding { get; private set; } = false;
    
    private float _savedAcceleration;
    private float _originalAirControl;
    private Vector3 _targetScale;
    private Vector3 _vel;
    private Vector3 _slideStartDir;
    private float _slideStartSpeed;
    private float _slideTargetSpeed;
    private float _newSpeed;
    private Coroutine _slideCoroutine;
    private bool _isInSlideRoutine;
    private float _originalSpeed;
    private Vector3 _originalScale;
    public UnityEvent OnSlide;
    public UnityEvent OnSlideEnd;
    public void StartSliding(Vector3 SlideDir)
    {
        if (IsSliding || !IsGrounded) return;

        _targetScale = _originalScale * _slideHeightMultiplier;
        _vel = Rigidbody.linearVelocity;
        _slideStartDir = _vel.normalized;
        _slideStartSpeed = Mathf.Max(0.01f, _vel.magnitude);
        _slideTargetSpeed = _slideStartSpeed * _slideSpeedMultiplier;
        //_slideBoostTimer = 0f;

       _slideCoroutine = StartCoroutine(SlideRoutine(SlideDir));

    }

    private IEnumerator SlideRoutine(Vector3 dir)
    {
        IsSliding = true;
        OnSlide.Invoke();
        Acceleration = 0f;
        _isInSlideRoutine = true;
        //Vector3 Vel = Rigidbody.linearVelocity;
        //IsSliding = true;
        float speedUpTimer = 0f;
        
        //SFx
        if (!_slideEnterReference.IsNull)
        {
            RuntimeManager.PlayOneShotAttached(_slideEnterReference, gameObject);
        }
        
        while (speedUpTimer < _slideAcceleDuration)
        {
            speedUpTimer += Time.unscaledDeltaTime;

            float accProg = Mathf.SmoothStep(0f, 1f, speedUpTimer / _slideAcceleDuration);

            float currentSpeed = Mathf.Lerp(_slideStartSpeed, _slideTargetSpeed, accProg);
            float heightY = Mathf.Lerp(_originalScale.y, _targetScale.y, accProg);

            _vel = _slideStartDir * currentSpeed;
            //Debug.Log(_vel);
            _vel.y = Rigidbody.linearVelocity.y;

            Rigidbody.linearVelocity = _vel;
            transform.localScale = new Vector3(_originalScale.x, heightY, _originalScale.z);

            yield return null;
        }


        //var waitFixed = new WaitForFixedUpdate();

        while (Rigidbody.linearVelocity.magnitude > _stopSlideSpeedThreshold)
        {
            _vel = Rigidbody.linearVelocity;

            float speed = _vel.magnitude;
            Vector3 speedDir = _vel.normalized;
            Vector3 moveinput = MoveInput.normalized;

            if (MoveInput.magnitude > 0.0001f) // if player is doing the mirco adjustment, apply it to the player Velocity
            {
                // the cos0 between current speed vector and moveInput Vector, if same direction > 0, if reverse direction < 0 
                float dot = Vector3.Dot(speedDir, moveinput);

                // Calculate the perpendicular amount that the moveInput on the Speed Vector, basically the 'Side' portion relatively to the speed vector 
                // 0 means no 'side' effect , 1 means vertical to the speed vector.
                float perpAmount = Mathf.Sqrt(Mathf.Clamp01(1f - dot * dot));

                //convert the steering Degress to Radian based on the 'perpendicular amount' calculated aboved
                float maxRad = _slideSteerDegPerSec * Mathf.Deg2Rad * Time.deltaTime * perpAmount;

                // calculate the new speed vector 
                _vel = Vector3.RotateTowards(_vel, moveinput * speed, maxRad, float.MaxValue);

                float extraBrake = (dot < 0) ? _slideBrakeStrength * (-dot) : 0f; // the more dot close to -1, the more brake there is
                _newSpeed = Mathf.Max(0f, speed - (_slideFriction + extraBrake) * Time.deltaTime);        
            }
            else
            {
                _newSpeed = Mathf.Max(0f, speed - _slideFriction * Time.deltaTime);
            }
            if (_slideEmitter&& IsGrounded)
            {
                _slideEmitter.SetParameter("SlidingFinished", 0);
                if (!_slideEmitter.IsPlaying())_slideEmitter.Play();
            }

            _vel = _vel.normalized * _newSpeed;
            Rigidbody.linearVelocity = new Vector3(_vel.x, Rigidbody.linearVelocity.y, _vel.z);
            yield return null;
        }

        //Debug.Log("not enough speed stop slide");
        StartCoroutine(StopSlideRoutine());
        _isInSlideRoutine = false;
    }


    public IEnumerator StopSlideRoutine()
    {

        //sfx
        if (!_slideExitReference.IsNull)
        {
            RuntimeManager.PlayOneShotAttached(_slideExitReference, gameObject);
        }

        if (_slideEmitter)
        {
            _slideEmitter.SetParameter("SlidingFinished", 1);
        }
        
        Acceleration = _savedAcceleration;
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _isInSlideRoutine = false ;
        float resetTimer = 0f;
        while (resetTimer < 0.2f && IsSliding)
        {
            resetTimer += Time.deltaTime;
            float resetProg = Mathf.SmoothStep(0f, 1f, resetTimer / 0.3f);
            float heightY = Mathf.Lerp(transform.localScale.y, _originalScale.y, resetProg);
            transform.localScale = new Vector3(transform.localScale.x, heightY, transform.localScale.z);
            yield return null;
        }

        IsSliding = false;
        transform.localScale = _originalScale;
        _slideProgPrec?.Invoke(1f);
        OnSlideEnd.Invoke();
    }

    public void StopSliding()
    {
        if (!IsSliding) return;
        StartCoroutine(StopSlideRoutine());
    }

    private void UpdateSlideCoolDown()
    {
        float hi = _slideTargetSpeed;
        float lo = _stopSlideSpeedThreshold;

        float p = (hi > lo) ? Mathf.InverseLerp(hi, lo, HorizontalSpeed) : 1f;
        _slideProgPrec?.Invoke(p);

    }

    #endregion

    #region Diving

    [Header("Diving Setting")]
    [SerializeField] private float _gravityMutiplier = 2f;
    private bool _isDiving;
    private float _gravity;
    
    public void TryDiving(bool isTryDiving)
    { 
        _isDiving = isTryDiving;
    }

    #endregion

    #region ghostmode
    public void GhostMove(Vector3 _ghostVel)
    {
        Rigidbody.MovePosition(Rigidbody.position + _ghostVel * Time.fixedDeltaTime);
    }
    #endregion

    #region Crouch


    [Header("Crouch Setting")]
    [SerializeField] private float _crouchGoDownDur = 0.2f;
    [SerializeField] private float _standUpDur = 0.2f;
    [SerializeField] private float _crouchHeightMultiplier = 0.5f;
    [SerializeField] private float _crouchSpeedMultiplier = 0.3f;
    [SerializeField] private float standCheckPadding = 0.02f;
    [SerializeField] private Transform headRayOrigin;
    [SerializeField] private EventReference _crouchStartEvent;
    [SerializeField] private EventReference _crouchEndEvent;
    
    private CapsuleCollider _capsule;

    public bool IsCrouching;
    
    public void StartCrouch(bool isTryCrouch)
    {
        if(!IsGrounded) return;
        _targetScale = _originalScale * _crouchHeightMultiplier;
        if (isTryCrouch ) StartCoroutine(CrouchGoDownRoutine());
        else if(!isTryCrouch ) StartCoroutine(StandUpRoutine());
        
    }

    private IEnumerator CrouchGoDownRoutine()
    {
        //play sfx
        if (!_crouchStartEvent.IsNull)
        {
            RuntimeManager.PlayOneShotAttached(_crouchStartEvent, gameObject);
        }
        
        IsCrouching = true;
        StopCoroutine(StandUpRoutine());
        Speed = Speed * _crouchSpeedMultiplier;


        float goDownTimer = 0f;
        while (goDownTimer < _crouchGoDownDur)
        {
            goDownTimer += Time.unscaledDeltaTime;

            float accProg = Mathf.SmoothStep(0f, 1f, goDownTimer / _crouchGoDownDur);
            float heightY = Mathf.Lerp(_originalScale.y, _targetScale.y, accProg);
            transform.localScale = new Vector3(_originalScale.x, heightY, _originalScale.z);
            yield return null;
        }

    }


    public IEnumerator StandUpRoutine()
    {
        StopCoroutine(CrouchGoDownRoutine());       
        //Play sfx
        if (!_crouchEndEvent.IsNull)
        {
            RuntimeManager.PlayOneShotAttached(_crouchEndEvent, gameObject);
        }

        float resetTimer = 0f;
        while (resetTimer < _standUpDur)
        {
            if (!CanStandUp())
            {
                
                //Debug.Log(false);
                yield return null;
                continue;
            }


            resetTimer += Time.deltaTime;
            float resetProg = Mathf.SmoothStep(0f, 1f, resetTimer / _standUpDur);
            float heightY = Mathf.Lerp(transform.localScale.y, _originalScale.y, resetProg);
            transform.localScale = new Vector3(transform.localScale.x, heightY, transform.localScale.z);
            yield return null;
        }

        transform.localScale = _originalScale;
        Speed = _originalSpeed;
        IsCrouching = false;
    }

    private bool CanStandUpAtScaleY(float targetScaleY)
    {
        if (headRayOrigin == null || _capsule == null) return true;

        float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float scaleYNow = Mathf.Abs(transform.lossyScale.y);
        float scaleYTarget = Mathf.Abs(_originalScale.y);

        float radius = _capsule.radius * scaleXZ;
        float heightNow = Mathf.Max(2f * radius, _capsule.height * scaleYNow);
        float heightTarget = Mathf.Max(2f * radius, _capsule.height * scaleYTarget);

        float needRaise = Mathf.Max(0f, heightTarget - heightNow); 
        if (needRaise <= 0f) return true; 

        Vector3 origin = headRayOrigin.position;
        Vector3 dirUp = transform.up;
        float distance = Mathf.Max(0f, needRaise - standCheckPadding);

        bool hit = Physics.Raycast(origin, dirUp, distance, GroundMask, QueryTriggerInteraction.Ignore);
        return !hit;
    }

    private bool CanStandUp()
    {
        return CanStandUpAtScaleY(_originalScale.y);
    }




    #endregion

    #region SlowMoMovement


    [Header("SlowMoMovement")]

    private bool hasSlowed = false;
    private float slowMoDivingMultilier = 1;
    public void ToggleSlowMoMovement(bool isInSlowMo)
    {
        if (hasSlowed == isInSlowMo) return;
        hasSlowed = isInSlowMo;

        slowMoDivingMultilier = hasSlowed ? playerMovementData.SlowMoDivingMulti : 1f;
        Speed = hasSlowed ? _originalSpeed * playerMovementData.SlowMoSpeedMulti : _originalSpeed;
        AirControl = hasSlowed ? _originalAirControl * playerMovementData.SlowMoAirControlMulti : _originalAirControl;
    }

    #endregion

    protected override void Awake()
    {
        base.Awake();
        _initialJumpHeight = JumpHeight;
        _originalScale = transform.localScale;
        _originalSpeed = Speed;
        _gravity = Gravity;
        _dashSavedAcc = Acceleration;
        _savedAcceleration = Acceleration;
        _originalAirControl = AirControl;
        _capsule = GetComponent<CapsuleCollider>();
    }

    protected override void FixedUpdate()
    {
        Gravity = _isDiving? _gravity * _gravityMutiplier * slowMoDivingMultilier : _gravity;
        if (IsDashing) UpdateDashCoolDown();
        if (IsSliding) UpdateSlideCoolDown();
        if (!IsGrounded && _isInSlideRoutine) StartCoroutine(StopSlideRoutine());
        CheckNavMeshLink();
        base.FixedUpdate();
        if (_postJumpTimer > 0f) _postJumpTimer -= Time.fixedDeltaTime;
        if (groundedSafe)
        {
            IsJumping = false;
        }
        if (_requestJump && _lastJumpFixedTick != Time.frameCount)
        {
            _lastJumpFixedTick = Time.frameCount;
            _requestJump = false;
            PerformJump();                 
            _postJumpTimer = _postJumpGroundingTime;  
        }
    }
    
    private void CheckNavMeshLink()
    {
        if (!NavMeshAgent.isOnOffMeshLink || IsLinkJumping) return;
        OffMeshLinkData linkData = NavMeshAgent.currentOffMeshLinkData;
        NavMeshLink link = NavMeshAgent.currentOffMeshLinkData.owner as NavMeshLink;
        if (link == null) return;
        if (!link.TryGetComponent(out LerpLink lerpLink)) return;
        //if (lerpLink.IsDrop(transform.position)) return;
        //if (Vector3.Distance(transform.position, linkData.endPos) < 0.5f) return; //Already very close to the end position, no need to jump
        Debug.Log("Lerp Jump");
        StartCoroutine(LerpJumpRoutine(transform.position, linkData.endPos, lerpLink));

    }

    private IEnumerator LerpJumpRoutine(Vector3 startPosition, Vector3 endPosition, LerpLink lerpLink)
    {
        IsLinkJumping = true;
        Rigidbody.isKinematic = true;
        Vector3 destination = NavMeshAgent.destination;
        WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
        float timer = 0f;
        float distance = Vector3.Distance(startPosition, endPosition);
        float duration = distance / (Speed * MoveSpeedMultiplier) * lerpLink.TimeModifier;
        while (timer < duration)
        {
            timer += Time.fixedDeltaTime;
            float progress = timer / duration;
            Vector3 position = lerpLink.GetJumpPosition(startPosition, endPosition, progress);
            HandleMove(position);
            yield return waitForFixedUpdate;
        }

        Rigidbody.isKinematic = false;
        NavMeshAgent.Warp(Rigidbody.position); // ensure the agent is exactly at the rigidbody position
        MoveTo(destination); // resume to the original destination
        IsLinkJumping = false;
    }
    
    //A try move method to return if the target position is valid on navmesh. If not valid the character will continue original movement.
    public bool TryMoveTo(Vector3 position)
    {
        if (!NavMeshAgent.isOnNavMesh) PullToNavMesh();
        NavMeshHit hit;
        bool validPosition = NavMesh.SamplePosition(position, out hit, 1f, NavMesh.AllAreas);
        if (!validPosition) return false;
        if (!NavMeshAgent.isActiveAndEnabled || !NavMeshAgent.isOnNavMesh) return false;
        return NavMeshAgent.SetDestination(position);
    }
    
    public void CompleteStop()
    {
        if (Rigidbody != null)
        {
            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
        }
        if (NavMeshAgent.enabled) NavMeshAgent.ResetPath();
        Stop();
    }

    public void HandleMove(Vector3 position, bool warpAgent = false)
    {
        transform.position = position;
        Rigidbody.MovePosition(position);
        SetLookPosition(position);
        if (warpAgent) NavMeshAgent.Warp(Rigidbody.position);
    }

    public void PullToNavMesh()
    {
        if (NavMeshAgent.isOnNavMesh) return;
        if (!IsGrounded) return;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Rigidbody.MovePosition(hit.position);
            NavMeshAgent.Warp(hit.position);
        }
    }

    public void HandleRotate(Quaternion rotation)
    {
        Rigidbody.MoveRotation(rotation);
        transform.rotation = rotation;
    }

    public void SetKinematic(bool kinematic)
    {
        Rigidbody.isKinematic = kinematic;
    }

    public void SetColliderEnabled(bool enabled)
    {
        _capsule.enabled = enabled;
    }

    #region FootstepSound

    public override void FootstepAnimEvent(AnimationEvent animationEvent)
    {
        if (IsDashing || IsSliding || IsJumping || IsLinkJumping || IsCrouching) return;
        base.FootstepAnimEvent(animationEvent);
    }

    #endregion

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.DrawRay(transform.position + new Vector3(0, 0.3f, 0), new Vector3(0, -1, 0)* 1f);
    }
}
