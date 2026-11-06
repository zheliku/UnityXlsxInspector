using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XlsxInspector
{
    // UI Toolkit 只负责取得真实的 Inspector 视口，表格和交互继续复用现有 IMGUI 实现。
    public partial class SpreadsheetInspector
    {
        private IMGUIContainer _inspectorElement; // 当前 Inspector 的 IMGUI 容器，用于换算网格位置。
        private ScrollView _inspectorScrollView; // Unity Inspector 外层滚动区域，不是表格自己的滚动区域。
        private VisualElement _inspectorViewport; // 固定大小的可见窗口区域，内容拉长不会增加它的高度。
        private Rect _gridRect; // 最近一次重绘得到的网格矩形，相对于 IMGUI 容器。
        private float _gridFooterHeight = 90; // 拖动条、Sheet 标签和状态文字的实际高度，首次重绘前预留 90 像素。
        private float _maxGridHeight = 440; // 根据当前窗口和工具栏位置计算的网格高度上限。

        /// <summary>创建 Inspector 容器，使用实际视口约束表格高度，避免拖动条离开窗口。</summary>
        /// <returns>包裹原有 IMGUI 绘制的 UI 元素；普通资源继续使用原生 Inspector 的创建路径。</returns>
        public override VisualElement CreateInspectorGUI()
        {
            if (_defaultInspector != null)
            {
                return _defaultInspector.CreateInspectorGUI();
            }

            _inspectorElement = new IMGUIContainer(OnInspectorGUI);
            _inspectorElement.RegisterCallback<AttachToPanelEvent>(OnInspectorAttached);
            _inspectorElement.RegisterCallback<DetachFromPanelEvent>(OnInspectorDetached);
            _inspectorElement.RegisterCallback<GeometryChangedEvent>(OnInspectorGeometryChanged);
            return _inspectorElement;
        }

        // 绑定当前实例的祖先视口，不能使用 focusedWindow：用户可能正在另一个 Inspector 或 Project 中操作。
        private void OnInspectorAttached(AttachToPanelEvent current)
        {
            _inspectorScrollView = _inspectorElement.GetFirstAncestorOfType<ScrollView>();
            _inspectorViewport = _inspectorScrollView == null
                ? _inspectorElement.panel.visualTree : _inspectorScrollView.contentViewport;
            _inspectorViewport.RegisterCallback<GeometryChangedEvent>(OnInspectorGeometryChanged);
            UpdatePanelHeightLimit();
        }

        private void OnInspectorDetached(DetachFromPanelEvent current)
        {
            EndSelectionDrag();
            _inspectorViewport.UnregisterCallback<GeometryChangedEvent>(OnInspectorGeometryChanged);
            _inspectorViewport = null;
            _inspectorScrollView = null;
        }

        private void OnInspectorGeometryChanged(GeometryChangedEvent current)
        {
            UpdatePanelHeightLimit();
        }

        private void UpdatePanelHeightLimit()
        {
            if (_inspectorViewport == null || !(_inspectorViewport.layout.height > 0)
                || !(_inspectorElement.layout.height > 0))
            {
                // 附加到窗口后，UI Toolkit 才会提供有效的布局尺寸。
                return;
            }

            Rect viewport = _inspectorViewport.worldBound;
            float outerScroll = _inspectorScrollView == null ? 0 : _inspectorScrollView.scrollOffset.y;
            float inspectorTop = Mathf.Max(viewport.yMin, _inspectorElement.worldBound.yMin + outerScroll);
            float available = viewport.yMax - inspectorTop - _gridRect.yMin - _gridFooterHeight - 4;
            _maxGridHeight = Mathf.Max(HeaderHeight + 32, available);
            SetPanelHeight(_gridHeight);
        }

        private void SetPanelHeight(float requestedHeight)
        {
            // 窗口较小时允许低于常规最小高度 180，优先保证底部拖动条仍能看到和操作。
            float height = Mathf.Clamp(requestedHeight, Mathf.Min(180, _maxGridHeight), _maxGridHeight);
            if (Mathf.Approximately(height, _gridHeight))
            {
                return;
            }

            _gridHeight = height;
            _inspectorElement?.MarkDirtyLayout();
            Repaint();
        }
    }
}
