using UnityEngine;
using GameEvents;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Unity.VisualScripting;
using System.Collections;
using FMODUnity;

public class SlowMotionController : MonoBehaviour
{
    [SerializeField, Range(0.01f, 1f), Tooltip("use the gun data first, if there is no player controller then use this")] private float slowScale = 0.2f;
    [field: SerializeField] public float MaxSlowMoDuration { get; private set; } = 3f;
    [SerializeField] private BoolEventAsset slowMotionEvent;
    [SerializeField] private FloatEventAsset slowMoProgressEvent;
    [SerializeField] private float slowMoThreshold= 0.1f;
    [SerializeField] private PlayerController playerController;
    [SerializeField, Tooltip("When exit the slowMo, how long is the transition")] private float exitTransitionTime = 0.7f;
    [field: SerializeField] public bool AutoActivate { get; set; }
    [field: SerializeField] public bool DoDecay { get; set; } = true;

    [Header("Recharge")]
    [SerializeField] private float rechargeDelay = 1.5f;   
    [SerializeField] private float rechargePerSecond = 1.0f;

    [Header("Feedback")]
    [SerializeField] VolumeProfile volumeProfile;

    [Header("SFX")] [SerializeField] private StudioEventEmitter slowMotionSnapShotEmitter;
    
    
    private ChromaticAberration chromaticAberration;
    private Coroutine SLowMoTranCor;

    public bool IsSlowing;
    private float baseFixedDT;
    private float rechargeCdTimer;
    public float Current { get; protected set; }
    private bool isGamePaused;
    private float slowMoTickMultiplier => playerController == null ? 1f : playerController.CurrentRangedWeapon.RangedWeaponData.slowMoTickMultiplier;
    private float slowMoScale => playerController == null ? slowScale : playerController.CurrentRangedWeapon.RangedWeaponData.slowMoScale;
    private float unscaledDeltaTime => isGamePaused? 0f: Time.unscaledDeltaTime * slowMoTickMultiplier;

    private void Awake()
    {
        baseFixedDT = Time.fixedDeltaTime;
        Current = MaxSlowMoDuration; 
        slowMoProgressEvent?.Invoke(1f);
        if (volumeProfile != null) volumeProfile.TryGet<ChromaticAberration>(out chromaticAberration);
        if (chromaticAberration != null) chromaticAberration.intensity.Override(0f);
        if (playerController == null) playerController = GetComponent<PlayerController>();
        PauseTimeControl.GamePaused += OnGamePaued;
    }

    private void OnEnable() 
    { 
        //Change to called by playercontroller
        //slowMotionEvent?.AddListener(OnSlowEvent); 
    }
    private void OnDisable() 
    {
        //Change to called by playercontroller
        //slowMotionEvent?.RemoveListener(OnSlowEvent); 
        PauseTimeControl.GamePaused -= OnGamePaued;
    }

    private void Update()
    {
        if (IsSlowing)
        {
            // real time
            if (Time.timeScale != 0 && DoDecay)
            {
                Current -= unscaledDeltaTime;
                if (Current <= 0f) ExitSlowMo();
            }
        }
        else
        {
            // cool down timer
            if (rechargeCdTimer > 0f)
            {
                rechargeCdTimer -= Time.deltaTime;
            }
            else if (Current < MaxSlowMoDuration)
            {
                // recharge
                Current += rechargePerSecond * Time.deltaTime;
                if (Current > MaxSlowMoDuration) Current = MaxSlowMoDuration;
            }
        }

        // ui
        slowMoProgressEvent?.Invoke(Mathf.Clamp01(Current / MaxSlowMoDuration));
    }

    public void OnSlowEvent(bool enter)
    {
        if (enter) EnterSlowMo();
        else ExitSlowMo();
    }

    private void OnGamePaued(bool onGamePaused)
    {
        isGamePaused = onGamePaused;
    }

    public void EnterSlowMo()
    {

        if (IsSlowing) return;
        if (Current <= 0f) return; 
        IsSlowing = true;
        if (SLowMoTranCor != null) StopCoroutine(SLowMoTranCor);
        //StartCoroutine(SlowMoLerp(slowMoScale, 1f));

        if (chromaticAberration != null) chromaticAberration.intensity.Override(1f);
        Time.timeScale = slowMoScale;
        Time.fixedDeltaTime = baseFixedDT;
        
        OverrideSoundPitch(slowMoScale);
    }

    public void ExitSlowMo()
    {
        if (!IsSlowing) return;
        IsSlowing = false;

        if (!isGamePaused) SLowMoTranCor = StartCoroutine(SlowMoLerp(1f, 0f));
       // if (chromaticAberration != null) chromaticAberration.intensity.Override(0f);
        //if(!isGamePaused)Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDT;
        rechargeCdTimer = rechargeDelay; 

    }

    //public float Progress01 => Mathf.Clamp01(current / maxSlowMoDuration);
    public bool CanEnter => !IsSlowing && ( Current > 0f);

    private IEnumerator SlowMoLerp(float targetTimeScale, float targetCBInten)
    {
        float initTimeScale = Time.timeScale;
        float initCBInten = chromaticAberration != null ? chromaticAberration.intensity.value : 0f;
        float currentTimeScale;
        float currentCBInten;
        float t = 0;
        while (t < exitTransitionTime)
        {
            if (isGamePaused)
            {
                yield return new WaitWhile(() => isGamePaused);
                
                continue;
            }

            t += Time.unscaledDeltaTime;
            float prog = Mathf.SmoothStep(0, 1, t/exitTransitionTime);
            currentTimeScale = Mathf.Lerp(initTimeScale, targetTimeScale, prog);
            Time.timeScale = currentTimeScale;
            if (chromaticAberration != null)
            {
                currentCBInten = Mathf.Lerp(initCBInten, targetCBInten, prog);
                chromaticAberration.intensity.Override(currentCBInten);
            }
            OverrideSoundPitch(currentTimeScale);
            
            yield return null;

        }
        Time.timeScale = targetTimeScale;
        if (chromaticAberration != null) chromaticAberration.intensity.Override(targetCBInten);
        OverrideSoundPitch(targetTimeScale);
    }

    public void RechargeSlowMo(float amount)
    {
        Current = Mathf.Min(Current + amount, MaxSlowMoDuration);
        slowMoProgressEvent.Invoke(Mathf.Clamp01(Current / MaxSlowMoDuration));
    }

    public void OverrideSoundPitch(float gameSpeed)
    {
        if (slowMotionSnapShotEmitter)
        {
            slowMotionSnapShotEmitter.SetParameter("GameSpeed", gameSpeed);
            slowMotionSnapShotEmitter.Play();
            
        }
    }

}
