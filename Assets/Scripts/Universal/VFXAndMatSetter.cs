using System;
using GameEvents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;

public class VFXAndMatSetter : MonoBehaviour
{
    [SerializeField] private bool hasVFX = false;
    [SerializeField] private bool hasMat = false;
    [SerializeField] private bool useEvent = false;
    [SerializeField] private PropertyType propertyType = PropertyType.Float;
    [SerializeField, Range(0f, 1f), ShowIf("propertyType", PropertyType.Float)] private float _floatValue = 0f;
    [SerializeField, ShowIf("propertyType", PropertyType.Bool)] private bool _boolState = false;

    [SerializeField,ShowIfGroup("FloatGroup", Condition = "@useEvent && propertyType == PropertyType.Float")][BoxGroup("FloatGroup/Setting", showLabel: false)] 
    private FloatEventAsset _floatEventAsset;
    [SerializeField,ShowIfGroup("BoolGroup", Condition = "@useEvent && propertyType == PropertyType.Bool")][BoxGroup("BoolGroup/Setting", showLabel: false)] 
    private BoolEventAsset _boolEventAsset;
    [BoxGroup("FloatGroup/Setting")]
    public UnityEvent<float> OnFloatGameEventInvoked;
    [BoxGroup("BoolGroup/Setting")] 
    public UnityEvent<bool> OnBoolGameEventInvoked;

    [SerializeField, ShowIfGroup("hasVFX"), TitleGroup("hasVFX/VFX Group", BoldTitle = true)] private VisualEffect[] _vfxs;
    [SerializeField, TitleGroup("hasVFX/VFX Group")] private string _vfxPropertyName;
    [SerializeField, ShowIfGroup("hasMat"), TitleGroup("hasMat/MatGroup", BoldTitle = true), TabGroup("hasMat/MatGroup/Setting", "Setting")] private Renderer[] _renderers;
    [SerializeField, TitleGroup("hasMat/MatGroup"), TabGroup("hasMat/MatGroup/Setting", "Setting")] private string _matFloatPropertyName;
    [SerializeField, TitleGroup("hasMat/MatGroup"), TabGroup("hasMat/MatGroup/Setting", "Setting")] private int _matIndex = 0;
    [SerializeField, TitleGroup("hasMat/MatGroup"), TabGroup("hasMat/MatGroup/Setting", "Utilities")] private Material _mat;
    [SerializeField, TitleGroup("hasMat/MatGroup"), TabGroup("hasMat/MatGroup/Setting", "Utilities")] private Transform root;

    private int? _matPropertyId;
    private int? _vfxPropertyId;
    private bool newBoolState;
    public static bool IsEditMode
    {
#if UNITY_EDITOR
        get => !Application.isPlaying;
#else
    get => false;
#endif
    }

    private void OnEnable()
    {
        switch (propertyType)
        {
            case PropertyType.Float:
                 _floatEventAsset?.AddListener(FloatEventInvoked);
                break;
                
            case PropertyType.Bool:
                 _boolEventAsset?.AddListener(BoolEventInvoked);
                break;    
        }    
    }

    private void OnDisable()
    {
        _floatEventAsset?.RemoveListener(FloatEventInvoked);
        _boolEventAsset?.RemoveListener(BoolEventInvoked);
    }

    private void FloatEventInvoked(float param)
    {
        OnFloatGameEventInvoked.Invoke(param);
    }

    private void BoolEventInvoked(bool param)
    {
        OnBoolGameEventInvoked.Invoke(param);
    }

    public int MatPropertyint
    {
        get
        {
            if (!IsEditMode && !_matPropertyId.HasValue || IsEditMode)
                _matPropertyId = Shader.PropertyToID(_matFloatPropertyName);
            return _matPropertyId.Value;
        }
    }

    public int VFXPropertyint
    {
        get
        {
            if (!IsEditMode && !_vfxPropertyId.HasValue || IsEditMode)
                _vfxPropertyId = Shader.PropertyToID(_vfxPropertyName);
            return _vfxPropertyId.Value;
        }
    }


#if UNITY_EDITOR
    private void OnValidate() 
    {
        switch (propertyType)
        { 
            case PropertyType.Float: FloatValue = _floatValue;break;
            case PropertyType.Bool: BoolValue = _boolState;break;
        }
    }
#endif
    public float FloatValue //Called by the UnityEvent
    { 
        get => _floatValue;
        set
        {
            _floatValue = value;
            if(hasMat) SetMatFloatProperty(value);
            if(hasVFX) SetVFXFloatProperty(value);
        }   
    }
    public bool BoolValue //Called by the UnityEvent
    {
        get => _boolState;
        set
        {
            _boolState = value;
            if (hasMat) SetMatBoolProperty(value);
            if (hasVFX) SetVFXBoolProperty(value);
        }
    }

    #region SetFloatProperty
    public void SetVFXFloatProperty(float param)
    {
        if (_vfxs == null) return;
        foreach (var vfx in _vfxs)
        {
            if (vfx == null) continue;
            if (!vfx.HasFloat(VFXPropertyint)) continue;
            vfx.SetFloat(VFXPropertyint, param);
        }
    }

    public void SetVFXFloatProperty(float param,string propertyName)
    {
        _vfxPropertyId = null;
        _vfxPropertyName = propertyName;
        SetVFXFloatProperty(param);
    }

    public void SetMatFloatProperty(float param)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;
            var mats = IsEditMode ? renderer.sharedMaterials : renderer.materials;
            if (_matIndex < 0 || _matIndex >= mats.Length) continue;
            var mat = mats[_matIndex];
            if (!mat.HasFloat(MatPropertyint)) continue;
            mat.SetFloat(MatPropertyint, param);
        }

    }

    public void SetMatFloatProperty(float param, string propertyName)
    {
        _matPropertyId = null;
        _matFloatPropertyName = propertyName;
        SetMatFloatProperty(param);
    }
    #endregion


    #region SetBoolProperty
    public void SetVFXBoolProperty(bool state)
    {
        float value = state ? 1 : 0;
        SetVFXFloatProperty(value);
    }

    public void SetVFXBoolProperty(bool state, string propertyName)
    {
        _vfxPropertyId = null;
        _vfxPropertyName = propertyName;
        SetVFXBoolProperty(state);
    }
    public void SetMatBoolProperty(bool state)
    {
        if (state == newBoolState) return;
        newBoolState = state;
        float value = newBoolState ? 1f : 0f;
        SetMatFloatProperty(value);
    }

    public void SetMatboolProperty(bool state, string propertyName)
    {
        _matPropertyId = null;
        _matFloatPropertyName = propertyName;
        SetMatBoolProperty(state);
    }
    #endregion

#if UNITY_EDITOR
    [TitleGroup("hasMat/MatGroup"), TabGroup("hasMat/MatGroup/Setting", "Utilities"), Button("Find Mesh Renderer Based on Root"), GUIColor(0, 1, 0)]
    public void FindMeshRendererBasedOnRoot()
    {

        _renderers = root.GetComponentsInChildren<MeshRenderer>(true);

    }

    [TitleGroup("hasMat/MatGroup"), TabGroup("hasMat/MatGroup/Setting", "Utilities"),Button("Overwrite listed Mesh Renderer Material Based on MatIndex"), GUIColor(0, 1, 0)]
    public void OverWriteRendererMatBasedOnIndex()
    {
        foreach (var render in _renderers)
        {
            var currentMat = render.materials;
            currentMat[_matIndex] = _mat;
            render.materials = currentMat;

        }

        
    }
#endif
    private enum PropertyType 
    { 
        Float,
        Bool
    }

}
