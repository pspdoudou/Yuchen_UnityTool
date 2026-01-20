using GameEvents;
using Sirenix.OdinInspector;
using UIComponents;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class GunRecoil : MonoBehaviour
{
    [SerializeField] private MouseSensData _sensData;
    [SerializeField, Tooltip("Mouse Sensitivity")] private float _mouseSens = 0.05f;
    [SerializeField] private GunManager _gunManager;

    [SerializeField] private CinemachinePanTilt _camPanTilt;
    [SerializeField] private CinemachineCamera _camCamera;
    [SerializeField] private InputActionReference _lookInputAction;
    [SerializeField] private float _minTilt = -80f;
    [SerializeField] private float _maxTilt = 80f;
    [SerializeField] private BoolEventAsset _slowMoEvent;

    //hip fire recoil 
    private float _recoilX => _gunManager.RecoilX;
    private float _recoilY => _gunManager.RecoilY;
    private float _recoilZ => _gunManager.RecoilZ;
    //ADS fire recoil 
    private float _aimRecoilX => _gunManager.AimRecoilX;
    private float _aimRecoilY => _gunManager.AimRecoilY;
    private float _aimRecoilZ => _gunManager.AimRecoilZ;
    //SlowMo Recoil
    private float _slowMocoilX => _gunManager.SlowMoRecoilX;
    private float _slowMoRecoilY => _gunManager.SlowMoRecoilY;
    private float _slowMoRecoilZ => _gunManager.SlowMoRecoilZ;

    //snappiness
    private float _snappiness => _gunManager.Snappiness;
    private float _returnSpeed => _gunManager.ReturnSpeed;


    private Vector3 _currentRotation;
    [ShowInInspector]private Vector3 _targetRotation;
    private float _rotationByMouseX;
    private float _rotationByMouseY;
    private float _initFOV;
    private float _dampVel;
    private float _targetFOV;
    private bool _isZooming;
    private float _adsTime;
    private bool _isInSlowMo;

    private void Awake()
    {
        if (_gunManager == null) _gunManager = GetComponentInParent<GunManager>();
        if (_camPanTilt == null) _camPanTilt = GetComponent<CinemachinePanTilt>();
        if (_camCamera == null) _camCamera = GetComponent<CinemachineCamera>();
        if (_lookInputAction == null) Debug.LogWarning("There is no PlayerInput Reference in the GunRecol Script, Can NOT Move the camera without it!");
        if (_camCamera != null) _initFOV = _camCamera.Lens.FieldOfView;
        if (_sensData != null)
        {
            _mouseSens = _sensData.HipSens;
            _sensData.OnDataChanged += ApplySensitivity;
        }
        if (_slowMoEvent != null) _slowMoEvent.AddListener(ApplySlowMoRecoil);
    }

    private void ApplySlowMoRecoil(bool isInSlowMo)
    {
        _isInSlowMo = isInSlowMo;
    }

    private void OnDisable()
    {
        if (_sensData != null) _sensData.OnDataChanged -= ApplySensitivity;
        if (_slowMoEvent != null) _slowMoEvent.RemoveListener(ApplySlowMoRecoil);

    }

    void Update()
    {

        _targetRotation = Vector3.Lerp(_targetRotation, Vector3.zero, _returnSpeed * Time.deltaTime); // calculate the target
        _currentRotation = Vector3.Slerp(_currentRotation,_targetRotation, _snappiness * Time.deltaTime); // lerp the current rotation to the target rotation
        CineMachineRotate(); // Mouse control camera rotation

        FOVZoom(); // ADS Zoom
    }

    private float GetActiveSensitivity() // sensitivity from the setting menu
    {
        if (_sensData == null) return 0.05f;

        if (!_gunManager.IsAimingIn) return _sensData.HipSens;

        else if (_gunManager.IsAimingIn)
        {
            switch (_gunManager.WeaponIndex)
            {
                case 0: return _sensData.SMGZoomSens;
                case 1: return _sensData.ShotGunZoomSens;
                case 2: return _sensData.SniperZoomSens;
                //default: return _sensData.MouseSensitivity;
            }
        }
        
       return _sensData.HipSens;
    }

    private void ApplySensitivity()
    {
        _mouseSens = _sensData.HipSens;
    }

    public void RecoilFire() // Called when the gun is firing
    {
       // Debug.Log("Recoil");
       if(_gunManager.IsAimingIn && !_isInSlowMo) _targetRotation += new Vector3(_aimRecoilX, Random.Range(-_aimRecoilY, _aimRecoilY), Random.Range(-_aimRecoilZ, _aimRecoilZ));

       else if(_isInSlowMo) _targetRotation += new Vector3(_slowMocoilX, Random.Range(-_slowMoRecoilY, _slowMoRecoilY), Random.Range(-_slowMoRecoilZ, _slowMoRecoilZ));

       else _targetRotation += new Vector3(_recoilX, Random.Range(-_recoilY, _recoilY), Random.Range(-_recoilZ, _recoilZ));

    }


    public void CineMachineRotate()
    {
        float sens = GetActiveSensitivity();
        //Debug.Log(sens);

        Vector2 mouseInput = _lookInputAction.action.ReadValue<Vector2>() * sens;
        
         _rotationByMouseX += mouseInput.x;
         _rotationByMouseY += -mouseInput.y;

        _rotationByMouseX = Mathf.Repeat(_rotationByMouseX, 360f);
        _rotationByMouseY = Mathf.Clamp(_rotationByMouseY, _minTilt, _maxTilt);


        _camPanTilt.PanAxis.Value = _rotationByMouseX + _currentRotation.y; // horizontal
        _camPanTilt.TiltAxis.Value = Mathf.Clamp(_rotationByMouseY + _currentRotation.x , _minTilt , _maxTilt); // vertical
        _camCamera.Lens.Dutch = _currentRotation.z; // roll

    }


    public void ADSCameraZoom(float ADSFOV, bool OnADS, float adsTime)
    {
        _adsTime = adsTime;
        _targetFOV = OnADS ? ADSFOV : _initFOV;
    }

    private void FOVZoom()
    {
        var lens = _camCamera.Lens;
        _isZooming = Mathf.Abs(_camCamera.Lens.FieldOfView - _targetFOV) < 0.02f ? false : true;
        if(_isZooming)lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, _targetFOV, ref _dampVel, _adsTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (!_isZooming) lens.FieldOfView = _targetFOV;
        _camCamera.Lens = lens;

    }

}
