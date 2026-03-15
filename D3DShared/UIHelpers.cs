using System.Drawing;
using System.Windows.Forms;
using System.Numerics;

namespace D3DShared;

/// <summary>
/// Shared UI components for D3D applications
/// </summary>
public static class UIHelpers
{
    /// <summary>
    /// Create a standard application form
    /// </summary>
    public static Form CreateMainForm(string title, int width = 1280, int height = 720)
    {
        return new Form
        {
            Text = title,
            ClientSize = new Size(width, height),
            StartPosition = FormStartPosition.CenterScreen,
            MinimumSize = new Size(400, 300),
            KeyPreview = true
        };
    }

    /// <summary>
    /// Create an FPS counter label (top right corner)
    /// </summary>
    public static Label CreateFpsLabel(Form form)
    {
        return new Label
        {
            Location = new Point(form.ClientSize.Width - 100, 10),
            Size = new Size(90, 20),
            Text = "FPS: --",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.TopRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
    }

    /// <summary>
    /// Create a counter label (next to FPS)
    /// </summary>
    public static Label CreateCounterLabel(Form form, string text = "Count: --")
    {
        return new Label
        {
            Location = new Point(form.ClientSize.Width - 220, 10),
            Size = new Size(115, 20),
            Text = text,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.TopRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
    }

    /// <summary>
    /// Create a device selector combobox (for use with D3D12Renderer)
    /// </summary>
    public static ComboBox CreateDeviceSelector(Form form, D3D12Renderer renderer, Action<int>? onDeviceChanged = null)
    {
        return CreateDeviceSelector(form, renderer.AvailableAdapters, renderer.SelectedAdapterIndex, onDeviceChanged);
    }

    /// <summary>
    /// Create a device selector combobox (for use with custom adapter list)
    /// </summary>
    public static ComboBox CreateDeviceSelector(Form form, IReadOnlyList<(int Index, string Name)> adapters, int selectedIndex, Action<int>? onDeviceChanged = null)
    {
        var deviceComboBox = new ComboBox
        {
            Location = new Point(form.ClientSize.Width - 300, 30),
            Size = new Size(290, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        foreach (var (index, name) in adapters)
            deviceComboBox.Items.Add(name);

        if (adapters.Count > 0 && selectedIndex >= 0 && selectedIndex < adapters.Count)
            deviceComboBox.SelectedIndex = selectedIndex;

        int currentSelectedIndex = selectedIndex;
        deviceComboBox.SelectedIndexChanged += (s, e) =>
        {
            if (deviceComboBox.SelectedIndex >= 0 && deviceComboBox.SelectedIndex != currentSelectedIndex)
            {
                currentSelectedIndex = deviceComboBox.SelectedIndex;
                onDeviceChanged?.Invoke(deviceComboBox.SelectedIndex);
            }
        };

        return deviceComboBox;
    }

    /// <summary>
    /// Standard slider spacing constant
    /// </summary>
    public const int SliderSpacing = 65;

    /// <summary>
    /// Create a labeled trackbar and add it to the parent control
    /// </summary>
    /// <param name="parent">Parent control to add the label and trackbar to</param>
    /// <param name="labelPrefix">Label text prefix (e.g., "Head Width")</param>
    /// <param name="yPos">Y position for the label</param>
    /// <param name="initialValue">Initial value (0.0 to 1.0)</param>
    /// <param name="onValueChanged">Callback when value changes, receives new float value (0.0 to 1.0)</param>
    /// <returns>The label and trackbar for later updates</returns>
    public static (Label Label, TrackBar Slider) CreateLabeledTrackBar(
        Control parent,
        string labelPrefix,
        int yPos,
        float initialValue,
        Action<float> onValueChanged)
    {
        var label = new Label
        {
            Location = new Point(5, yPos),
            Size = new Size(165, 18),
            Text = $"{labelPrefix}: {initialValue:F2}",
            Font = new Font("Segoe UI", 9)
        };
        parent.Controls.Add(label);

        var slider = new TrackBar
        {
            Location = new Point(5, yPos + 18),
            Size = new Size(165, 45),
            Minimum = 0,
            Maximum = 100,
            Value = (int)(initialValue * 100),
            TickFrequency = 10
        };
        slider.ValueChanged += (s, e) =>
        {
            float value = slider.Value / 100.0f;
            label.Text = $"{labelPrefix}: {value:F2}";
            onValueChanged(value);
        };
        parent.Controls.Add(slider);

        return (label, slider);
    }

    /// <summary>
    /// Create a labeled trackbar with custom range and conversion
    /// </summary>
    /// <param name="parent">Parent control to add the label and trackbar to</param>
    /// <param name="labelPrefix">Label text prefix (e.g., "Hair Density")</param>
    /// <param name="yPos">Y position for the label</param>
    /// <param name="min">Minimum slider value</param>
    /// <param name="max">Maximum slider value</param>
    /// <param name="initialValue">Initial slider value (integer)</param>
    /// <param name="valueToText">Function to convert slider value to label text</param>
    /// <param name="onValueChanged">Callback when value changes, receives slider integer value</param>
    /// <returns>The label and trackbar for later updates</returns>
    public static (Label Label, TrackBar Slider) CreateLabeledTrackBar(
        Control parent,
        string labelPrefix,
        int yPos,
        int min,
        int max,
        int initialValue,
        Func<int, string> valueToText,
        Action<int> onValueChanged)
    {
        var label = new Label
        {
            Location = new Point(5, yPos),
            Size = new Size(165, 18),
            Text = $"{labelPrefix}: {valueToText(initialValue)}",
            Font = new Font("Segoe UI", 9)
        };
        parent.Controls.Add(label);

        var slider = new TrackBar
        {
            Location = new Point(5, yPos + 18),
            Size = new Size(165, 45),
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(initialValue, min, max),
            TickFrequency = Math.Max(1, (max - min) / 10)
        };
        slider.ValueChanged += (s, e) =>
        {
            label.Text = $"{labelPrefix}: {valueToText(slider.Value)}";
            onValueChanged(slider.Value);
        };
        parent.Controls.Add(slider);

        return (label, slider);
    }

    /// <summary>
    /// Create a group box with a title
    /// </summary>
    public static GroupBox CreateGroupBox(string title, int x, int y, int width, int height)
    {
        return new GroupBox
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            Text = title,
            Font = new Font("Segoe UI", 9)
        };
    }

    /// <summary>
    /// FPS calculation helper
    /// </summary>
    public class FpsCounter
    {
        private int _frameCount;
        private DateTime _lastUpdate = DateTime.Now;
        private float _currentFps;

        public float CurrentFps => _currentFps;

        public bool Update()
        {
            _frameCount++;
            var now = DateTime.Now;

            if ((now - _lastUpdate).TotalSeconds >= 0.5)
            {
                _currentFps = _frameCount / (float)(now - _lastUpdate).TotalSeconds;
                _frameCount = 0;
                _lastUpdate = now;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Create a tabbed parameter control panel
    /// </summary>
    public static TabControl CreateTabbedParameterPanel(int x, int y, int width, int height)
    {
        return new TabControl
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            Font = new Font("Segoe UI", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
        };
    }

    /// <summary>
    /// Create a tab page with a scrollable parameter panel
    /// </summary>
    public static (TabPage Tab, FlowLayoutPanel Panel) CreateParameterTab(string title)
    {
        var tab = new TabPage(title)
        {
            AutoScroll = true,
            Padding = new Padding(3)
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0)
        };

        tab.Controls.Add(panel);
        return (tab, panel);
    }

    /// <summary>
    /// Create a collapsible parameter group within a flow panel
    /// </summary>
    public static ParameterGroup CreateParameterGroup(FlowLayoutPanel parent, string title, int sliderWidth = 160)
    {
        return new ParameterGroup(parent, title, sliderWidth);
    }
}

/// <summary>
/// A collapsible group of parameter sliders
/// </summary>
public class ParameterGroup
{
    private readonly Panel _container;
    private readonly Button _headerButton;
    private readonly FlowLayoutPanel _contentPanel;
    private readonly List<(Label Label, TrackBar Slider, int Index)> _sliders = new();
    private readonly int _sliderWidth;
    private bool _isExpanded = false;
    private const int SliderHeight = 70;  // Label(18) + margins + TrackBar(45) + spacing
    private const int HeaderHeight = 25;
    private const int ContentPadding = 25; // Extra padding at bottom

    public event Action<int, float>? ValueChanged;

    public ParameterGroup(FlowLayoutPanel parent, string title, int sliderWidth = 160)
    {
        _sliderWidth = sliderWidth;

        _container = new Panel
        {
            Width = sliderWidth + 10,
            Height = HeaderHeight,
            Margin = new Padding(0, 2, 0, 2)
        };

        _headerButton = new Button
        {
            Text = $"▶ {title}",
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Top,
            Height = HeaderHeight,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _headerButton.FlatAppearance.BorderSize = 0;
        _headerButton.Click += (s, e) => ToggleExpanded();

        _contentPanel = new FlowLayoutPanel
        {
            Location = new Point(0, HeaderHeight),
            Width = sliderWidth + 10,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            Visible = false,
            Padding = new Padding(5, 0, 0, 0)
        };

        _container.Controls.Add(_contentPanel);
        _container.Controls.Add(_headerButton);
        parent.Controls.Add(_container);
    }

    /// <summary>
    /// Add a slider to this group
    /// </summary>
    public void AddSlider(string label, int paramIndex, float initialValue = 0f,
        float rangeMin = -1f, float rangeMax = 1f)
    {
        const int Scale = 100;
        int min = (int)(rangeMin * Scale);
        int max = (int)(rangeMax * Scale);
        int initial = (int)(initialValue * Scale);
        int tickFreq = Math.Max(1, (max - min) / 4);

        var lblCtrl = new Label
        {
            Text = $"{label}: {initialValue:F2}",
            Width = _sliderWidth,
            Height = 18,
            Font = new Font("Segoe UI", 8),
            Margin = new Padding(0, 3, 0, 0)
        };

        var slider = new TrackBar
        {
            Minimum = min,
            Maximum = max,
            Value = initial,
            Width = _sliderWidth,
            Height = 45,
            TickFrequency = tickFreq,
            Margin = new Padding(0, 0, 0, 4)
        };

        int idx = paramIndex;
        slider.ValueChanged += (s, e) =>
        {
            float value = slider.Value / (float)Scale;
            lblCtrl.Text = $"{label}: {value:F2}";
            ValueChanged?.Invoke(idx, value);
        };

        _contentPanel.Controls.Add(lblCtrl);
        _contentPanel.Controls.Add(slider);
        _sliders.Add((lblCtrl, slider, paramIndex));

        UpdateContentHeight();
    }

    /// <summary>
    /// Add multiple sliders with auto-generated labels
    /// </summary>
    public void AddSliders(string labelPrefix, int startIndex, int count, float initialValue = 0.5f)
    {
        for (int i = 0; i < count; i++)
        {
            AddSlider($"{labelPrefix} {i + 1}", startIndex + i, initialValue);
        }
    }

    /// <summary>
    /// Set a slider value programmatically
    /// </summary>
    public void SetValue(int paramIndex, float value)
    {
        var item = _sliders.FirstOrDefault(s => s.Index == paramIndex);
        if (item.Slider != null)
        {
            item.Slider.Value = Math.Clamp((int)(value * 100), item.Slider.Minimum, item.Slider.Maximum);
        }
    }

    /// <summary>
    /// Reset all sliders to zero
    /// </summary>
    public void ResetAll(float value = 0f)
    {
        foreach (var (_, slider, _) in _sliders)
        {
            slider.Value = Math.Clamp((int)(value * 100), slider.Minimum, slider.Maximum);
        }
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        _contentPanel.Visible = _isExpanded;
        _headerButton.Text = (_isExpanded ? "▼ " : "▶ ") + _headerButton.Text.Substring(2);
        UpdateContainerHeight();
    }

    private void UpdateContentHeight()
    {
        int contentHeight = _sliders.Count * SliderHeight + ContentPadding;
        _contentPanel.Height = contentHeight;
        UpdateContainerHeight();
    }

    private void UpdateContainerHeight()
    {
        _container.Height = _isExpanded ? HeaderHeight + _contentPanel.Height : HeaderHeight;
    }

    /// <summary>
    /// Expand this group
    /// </summary>
    public void Expand()
    {
        if (!_isExpanded) ToggleExpanded();
    }

    /// <summary>
    /// Collapse this group
    /// </summary>
    public void Collapse()
    {
        if (_isExpanded) ToggleExpanded();
    }
}

/// <summary>
/// Manager for a complete parameter panel with tabs and groups
/// </summary>
public class ParameterPanelManager
{
    private readonly TabControl _tabControl;
    private readonly Dictionary<string, (TabPage Tab, FlowLayoutPanel Panel)> _tabs = new();
    private readonly Dictionary<string, List<ParameterGroup>> _groups = new();
    private readonly float[] _parameterValues;

    public event Action? ParametersChanged;

    public ParameterPanelManager(Control parent, int x, int y, int width, int height, int totalParameters)
    {
        _parameterValues = new float[totalParameters];

        _tabControl = UIHelpers.CreateTabbedParameterPanel(x, y, width, height);
        parent.Controls.Add(_tabControl);
    }

    /// <summary>
    /// Add a new tab
    /// </summary>
    public FlowLayoutPanel AddTab(string title)
    {
        var (tab, panel) = UIHelpers.CreateParameterTab(title);
        _tabControl.TabPages.Add(tab);
        _tabs[title] = (tab, panel);
        _groups[title] = new List<ParameterGroup>();
        return panel;
    }

    /// <summary>
    /// Add a parameter group to a tab
    /// </summary>
    public ParameterGroup AddGroup(string tabTitle, string groupTitle, int sliderWidth = 160)
    {
        if (!_tabs.TryGetValue(tabTitle, out var tabData))
            throw new ArgumentException($"Tab '{tabTitle}' not found");

        var group = UIHelpers.CreateParameterGroup(tabData.Panel, groupTitle, sliderWidth);
        group.ValueChanged += OnGroupValueChanged;
        _groups[tabTitle].Add(group);
        return group;
    }

    private void OnGroupValueChanged(int paramIndex, float value)
    {
        if (paramIndex >= 0 && paramIndex < _parameterValues.Length)
        {
            _parameterValues[paramIndex] = value;
            ParametersChanged?.Invoke();
        }
    }

    /// <summary>
    /// Get all parameter values
    /// </summary>
    public float[] GetAllValues() => _parameterValues;

    /// <summary>
    /// Get parameter values for a range
    /// </summary>
    public float[] GetValues(int startIndex, int count)
    {
        var result = new float[count];
        Array.Copy(_parameterValues, startIndex, result, 0, count);
        return result;
    }

    /// <summary>
    /// Set a parameter value
    /// </summary>
    public void SetValue(int index, float value)
    {
        if (index >= 0 && index < _parameterValues.Length)
        {
            _parameterValues[index] = value;
        }
    }

    /// <summary>
    /// Reset all parameters to zero
    /// </summary>
    public void ResetAll(float defaultValue = 0f)
    {
        Array.Fill(_parameterValues, defaultValue);
        foreach (var groupList in _groups.Values)
        {
            foreach (var group in groupList)
            {
                group.ResetAll(defaultValue);
            }
        }
        ParametersChanged?.Invoke();
    }

    /// <summary>
    /// Add a reset button below the tab control
    /// </summary>
    public Button AddResetButton(Control parent)
    {
        var btn = new Button
        {
            Text = "Reset to Default",
            Location = new Point(_tabControl.Left, _tabControl.Bottom + 5),
            Size = new Size(_tabControl.Width, 25),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        btn.Click += (s, e) => ResetAll();
        parent.Controls.Add(btn);
        return btn;
    }
}
