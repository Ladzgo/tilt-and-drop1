using System;
using TiltDrop.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TiltDrop.Game
{
    /// <summary>Swipe left/right anywhere (outside UI) to rotate; tap for targeted boosters.
    /// Uses the legacy Input API (touches are mirrored to the mouse API by default).</summary>
    public sealed class BoardInput : MonoBehaviour
    {
        public event Action<Rot> Rotate;
        public event Action<Vector2> Tap;
        public bool Active = true;

        Vector2 down;
        bool tracking;

        static bool OverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }

        void Update()
        {
            if (!Active) { tracking = false; return; }
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Rotate?.Invoke(Rot.CW);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Rotate?.Invoke(Rot.CCW);
#endif
            if (Input.GetMouseButtonDown(0))
            {
                tracking = !OverUI();
                down = Input.mousePosition;
            }
            else if (tracking && Input.GetMouseButtonUp(0))
            {
                tracking = false;
                Vector2 d = (Vector2)Input.mousePosition - down;
                float min = Screen.width * 0.06f;
                if (Mathf.Abs(d.x) > min && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.2f) Rotate?.Invoke(d.x > 0 ? Rot.CW : Rot.CCW);
                else if (d.magnitude < min * 0.5f) Tap?.Invoke(Input.mousePosition);
            }
        }
    }
}
