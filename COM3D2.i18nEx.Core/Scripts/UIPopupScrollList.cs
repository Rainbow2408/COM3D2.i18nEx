using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.i18nEx.Core.Scripts
{
    internal class UIPopupScrollList : UIPopupList
    {
        // 快取的欄位參照（使用 AccessTools.FieldRefAccess 以提升效能）
        private static readonly AccessTools.FieldRef<UIPopupList, GameObject> _mChildRef =
            AccessTools.FieldRefAccess<UIPopupList, GameObject>("mChild");
        private static readonly AccessTools.FieldRef<UIPopupList, UIPanel> _mPanelRef =
            AccessTools.FieldRefAccess<UIPopupList, UIPanel>("mPanel");
        private static readonly AccessTools.FieldRef<UIPopupList, UISprite> _mBackgroundRef =
            AccessTools.FieldRefAccess<UIPopupList, UISprite>("mBackground");
        private static readonly AccessTools.FieldRef<UIPopupList, UISprite> _mHighlightRef =
            AccessTools.FieldRefAccess<UIPopupList, UISprite>("mHighlight");
        private static readonly PropertyInfo _isValidProperty =
            AccessTools.Property(typeof(UIPopupList), "isValid");

        private static int mOpenFrame = 0;

        protected new void OnClick()
        {
            if (mOpenFrame == Time.frameCount) return;

            if (openOn == OpenOn.DoubleClick || openOn == OpenOn.Manual) return;
            if (openOn == OpenOn.RightClick && UICamera.currentTouchID != -2) return;
            Show();
        }

        public new void Show()
        {
            // Set position to below
            position = Position.Below;

            // Call original method
            base.Show();

            // 使用快取的欄位參照取得私有欄位值
            bool _isValid = (bool)_isValidProperty.GetValue(this, null);
            GameObject _mChild = _mChildRef(this);
            UIPanel _mPanel = _mPanelRef(this);
            UISprite _mBackground = _mBackgroundRef(this);
            UISprite _mHighlight = _mHighlightRef(this);

            // base.Show() Postfix
            if (enabled && NGUITools.GetActive(gameObject) && _mChild != null && atlas != null && _isValid && items.Count > 0)
            {
                mOpenFrame = Time.frameCount;

                // Calculate the dimensions of the object triggering the popup list so we can position it below it
                Vector3 min;
                Vector3 max;

                // Create the root object for the list
                Transform pTrans = transform;
                Transform t = _mChild.transform;
                t.parent = pTrans.parent;

                // Manually triggered popup list on some other game object
                if (openOn == OpenOn.Manual && UICamera.selectedObject != gameObject)
                {
                    StopCoroutine("CloseIfUnselected");
                    min = t.parent.InverseTransformPoint(_mPanel.anchorCamera.ScreenToWorldPoint(UICamera.lastTouchPosition));
                    max = min;
                    t.localPosition = min;
                    StartCoroutine("CloseIfUnselected");
                }
                else
                {
                    Bounds bounds = NGUIMath.CalculateRelativeWidgetBounds(pTrans.parent, pTrans, false, false);
                    min = bounds.min;
                    max = bounds.max;
                    t.localPosition = min;
                }
                t.localScale = Vector3.one;
                // If we need to place the popup list above the item, we need to reposition everything by the size of the list
                min = t.localPosition;
                max.x = min.x + _mBackground.width;
                max.y = min.y - _mBackground.height;
                max.z = min.z;

                // Ensure that everything fits into the panel's visible range
                Vector3 offset = _mPanel.CalculateConstrainOffset(min, max);
                t.localPosition += offset;

                //------------------------------------------------------------------------------------------------
                // Add Scroll View & Clip Region
                var _panel = _mChild.AddComponent<UIPanel>();
                _panel.depth = 1000000;
                _panel.sortingOrder = _mPanel.sortingOrder;
                _panel.clipping = UIDrawCall.Clipping.SoftClip;

                var unit = padding.y + (fontSize + padding.y) * 8 + _mBackground.border.y * 2;
                _panel.baseClipRegion = new Vector4
                {
                    x = _mBackground.width / 2,
                    z = _mBackground.width + 4,
                    y = -unit / 2 + 2,
                    w = unit + 4 + 4
                };

                var scroll_v = _mChild.AddComponent<UIScrollView>();
                scroll_v.movement = UIScrollView.Movement.Vertical;
                scroll_v.dragEffect = UIScrollView.DragEffect.Momentum;
                scroll_v.scrollWheelFactor = 0.8f;
                scroll_v.disableDragIfFits = true;

                for (int i = 0; i < _mChild.transform.childCount; i++)
                {
                    var child = _mChild.transform.GetChild(i).gameObject;
                    child.AddComponent<UIDragScrollView>();
                    child.GetComponent<TweenColor>().AddOnFinished(delegate ()
                    {
                        // Restart to make Scroll View work
                        child.SetActive(false);
                        child.SetActive(true);
                    });
                }
            }
        }
    }
}
