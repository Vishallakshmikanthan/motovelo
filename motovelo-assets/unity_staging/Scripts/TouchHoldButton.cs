using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Motovelo
{
    public class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool isHeld;
        public Action onDown;
        public Action onUp;
        public Image targetGraphic;
        public Color idleColor = new Color(1f, 1f, 1f, 0.22f);
        public Color pressedColor = new Color(1f, 1f, 1f, 0.52f);

        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (targetGraphic == null) targetGraphic = GetComponent<Image>();
            if (targetGraphic != null) targetGraphic.color = idleColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isHeld = true;
            transform.localScale = _baseScale * 0.94f;
            if (targetGraphic != null) targetGraphic.color = pressedColor;
            onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isHeld) Release();
        }

        private void Release()
        {
            isHeld = false;
            transform.localScale = _baseScale;
            if (targetGraphic != null) targetGraphic.color = idleColor;
            onUp?.Invoke();
        }
    }
}
