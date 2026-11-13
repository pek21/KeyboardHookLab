using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using KeyboardHookLab.Interop;

namespace KeyboardHookLab;

public sealed class MainForm : Form
{
    private readonly TextBox _logTextBox;
    private readonly CheckBox _hookCheckBox;
    private readonly CheckBox _functionKeysOnlyCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _counterLabel;
    private readonly KeyboardHook _keyboardHook;
    private int _eventCounter;
    private bool _isClosing;

    public MainForm()
    {
        Text = "Keyboard Hook Laboratory";
        Width = 900;
        Height = 550;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyPressed += OnHookKeyPressed;

        _logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10)
        };

        _hookCheckBox = new CheckBox
        {
            Text = "Включить low-level hook",
            AutoSize = true,
            Margin = new Padding(8)
        };

        _functionKeysOnlyCheckBox = new CheckBox
        {
            Text = "Только функциональные клавиши (F1-F12)",
            AutoSize = true,
            Margin = new Padding(8)
        };

        var clearButton = new Button
        {
            Text = "Очистить журнал",
            AutoSize = true
        };

        _statusLabel = new Label
        {
            Text = "Hook не установлен",
            AutoSize = true,
            Margin = new Padding(8),
            ForeColor = Color.Gray
        };

        _counterLabel = new Label
        {
            Text = "Событий: 0",
            AutoSize = true,
            Margin = new Padding(8),
            Font = new Font(Font, FontStyle.Bold)
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false
        };

        toolbar.Controls.Add(_hookCheckBox);
        toolbar.Controls.Add(_functionKeysOnlyCheckBox);
        toolbar.Controls.Add(clearButton);
        toolbar.Controls.Add(_statusLabel);
        toolbar.Controls.Add(_counterLabel);

        Controls.Add(_logTextBox);
        Controls.Add(toolbar);

        KeyDown += OnLocalKeyDown;
        KeyUp += OnLocalKeyUp;

        clearButton.Click += (_, _) =>
        {
            _logTextBox.Clear();
        };

        _hookCheckBox.CheckedChanged += OnHookCheckedChanged;
        _functionKeysOnlyCheckBox.CheckedChanged += (_, _) =>
        {
            AppendLog("System", "Фильтр функциональных клавиш изменён");
        };

        Activated += OnFormActivated;
        Deactivate += OnFormDeactivated;
        FormClosing += OnFormClosing;
    }

    private void OnLocalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _logTextBox.Clear();
            AppendLog("System", "Журнал очищен клавишей Escape");
            return;
        }

        AppendLog("WinForms KeyDown", e.KeyCode.ToString());
    }

    private void OnLocalKeyUp(object? sender, KeyEventArgs e)
    {
        AppendLog("WinForms KeyUp", e.KeyCode.ToString());
    }

    private void OnHookKeyPressed(Keys key, bool isKeyDown, bool isSystem)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        if (_functionKeysOnlyCheckBox.Checked)
        {
            if (key < Keys.F1 || key > Keys.F12)
            {
                return;
            }
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed)
                {
                    string eventType = isKeyDown ? "KeyDown" : "KeyUp";
                    string source = isSystem ? "Low-level hook (SYS)" : "Low-level hook";
                    AppendLog(source, $"{key} [{eventType}]");
                }
            }));
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Не удалось обновить журнал: {ex}");
        }
    }

    private void AppendLog(string source, string key)
    {
        string timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        _logTextBox.AppendText(
            $"[{timestamp}] [{source}] {key}" +
            Environment.NewLine);

        _eventCounter++;
        _counterLabel.Text = $"Событий: {_eventCounter}";
    }

    private void OnHookCheckedChanged(object? sender, EventArgs e)
    {
        if (_isClosing)
            return;

        if (_hookCheckBox.Checked)
        {
            if (Form.ActiveForm == this)
                StartHook();
        }
        else
        {
            StopHook();
        }
    }

    private void OnFormActivated(object? sender, EventArgs e)
    {
        if (_hookCheckBox.Checked && !_isClosing)
            StartHook();
    }

    private void OnFormDeactivated(object? sender, EventArgs e)
    {
        StopHook();
    }

    private void StartHook()
    {
        if (_keyboardHook.IsInstalled)
            return;

        try
        {
            _keyboardHook.Start();
            _statusLabel.Text = "Hook установлен";
            _statusLabel.ForeColor = Color.Green;
            AppendLog("System", "Hook установлен");
        }
        catch (Win32Exception ex)
        {
            _statusLabel.Text = "Ошибка установки hook";
            _statusLabel.ForeColor = Color.Red;
            AppendLog("Error", ex.Message);
            _hookCheckBox.Checked = false;
        }
    }

    private void StopHook()
    {
        if (!_keyboardHook.IsInstalled)
            return;

        try
        {
            _keyboardHook.Stop();
            _statusLabel.Text = "Hook не установлен";
            _statusLabel.ForeColor = Color.Gray;
            AppendLog("System", "Hook снят");
        }
        catch (Win32Exception ex)
        {
            _statusLabel.Text = "Ошибка снятия hook";
            _statusLabel.ForeColor = Color.Red;
            AppendLog("Error", ex.Message);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _isClosing = true;
        _hookCheckBox.Enabled = false;

        StopHook();

        try
        {
            _keyboardHook.Dispose();
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"Ошибка освобождения hook: {ex}");
        }
    }
}