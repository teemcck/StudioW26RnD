using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(TMP_Text))]
public sealed class TooltipLinkRelay : MonoBehaviour, IPointerExitHandler
{
    private TMP_Text _text;
    private Camera _uiCamera;
    private string _hoveredKey;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            _uiCamera = null;
        else if (canvas != null)
            _uiCamera = canvas.worldCamera;
    }

    private void Update()
    {
        if (_text == null) return;

        Vector3 mousePos = GetPointerScreenPosition();
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, mousePos, _uiCamera);

        if (linkIndex < 0)
        {
            if (_hoveredKey != null)
            {
                _hoveredKey = null;
                StatusTooltipController.Instance?.Hide();
            }
            return;
        }

        var info = _text.textInfo.linkInfo[linkIndex];
        string id = info.GetLinkID();
        if (id != _hoveredKey) _hoveredKey = id;

        StatusTooltipController.Instance?.Show(id, mousePos);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_hoveredKey != null)
        {
            _hoveredKey = null;
            StatusTooltipController.Instance?.Hide();
        }
    }

    private void OnDisable()
    {
        if (_hoveredKey != null)
        {
            _hoveredKey = null;
            StatusTooltipController.Instance?.Hide();
        }
    }

    private static Vector3 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
        if (Pointer.current != null) return Pointer.current.position.ReadValue();
        return Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }
}
