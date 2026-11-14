using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// 「设置 → 首选项...」窗口：在界面上修改 settings.ini 里的各项，
    /// 点「确定」后写回文件，并立刻套用差异面板字体。
    /// </summary>
    public class SettingsForm : Form
    {
        private NumericUpDown numMaxLines;
        private NumericUpDown numMaxDiffSizeKB;
        private NumericUpDown numPreviewKB;
        private NumericUpDown numRecentRepos;
        private NumericUpDown numFontSize;
        private CheckBox chkConfirmDangerous;
        private ComboBox cboFont;
        private RichTextBox preview;

        private static readonly Color HintGray = Color.FromArgb(110, 110, 110);

        public SettingsForm()
        {
            Text = "首选项";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(500, 420);
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildDiffGroup();
            BuildPreviewGroup();
            BuildGeneralGroup();
            BuildButtons();

            LoadValues();
        }

        /// <summary>点「确定」后写回的值，供调用方刷新界面用。</summary>
        public int AppliedFontSize { get; private set; }
        public string AppliedFontName { get; private set; }

        // ---------- 界面搭建 ----------

        private void BuildDiffGroup()
        {
            var group = new GroupBox
            {
                Text = "差异面板",
                Location = new Point(12, 12),
                Size = new Size(476, 178),
            };

            group.Controls.Add(LabelAt("最多渲染行数", 14, 25, 120));
            numMaxLines = NumberAt(140, 22, 84, 1, 10000000, 100);
            group.Controls.Add(numMaxLines);
            group.Controls.Add(HintAt("超出只显示前 N 行", 232, 25));

            group.Controls.Add(LabelAt("超过多少 KB 不显示", 14, 57, 120));
            numMaxDiffSizeKB = NumberAt(140, 54, 84, 0, 1000000, 256);
            group.Controls.Add(numMaxDiffSizeKB);
            group.Controls.Add(HintAt("0 = 不限制", 232, 57));

            group.Controls.Add(LabelAt("字体", 14, 89, 40));
            cboFont = new ComboBox
            {
                Location = new Point(56, 86),
                Size = new Size(276, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            FillFonts(cboFont);
            cboFont.SelectedIndexChanged += (s, e) => ApplyPreviewFont();
            group.Controls.Add(cboFont);

            group.Controls.Add(LabelAt("字号", 340, 89, 34));
            numFontSize = NumberAt(378, 86, 60, 6, 72, 1);
            numFontSize.ValueChanged += (s, e) => ApplyPreviewFont();
            group.Controls.Add(numFontSize);

            preview = new RichTextBox
            {
                Location = new Point(14, 118),
                Size = new Size(446, 52),
                ReadOnly = true,
                WordWrap = false,
                DetectUrls = false,
                HideSelection = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                BackColor = Color.White,
                Text = "",
            };
            DrawPreview();
            group.Controls.Add(preview);

            Controls.Add(group);
        }

        private void BuildPreviewGroup()
        {
            var group = new GroupBox
            {
                Text = "未跟踪文件预览",
                Location = new Point(12, 198),
                Size = new Size(476, 62),
            };
            group.Controls.Add(LabelAt("超过多少 KB 不显示", 14, 25, 120));
            numPreviewKB = NumberAt(140, 22, 90, 0, 1000000, 256);
            group.Controls.Add(numPreviewKB);
            group.Controls.Add(HintAt("0 = 不限制", 238, 25));
            Controls.Add(group);
        }

        private void BuildGeneralGroup()
        {
            var group = new GroupBox
            {
                Text = "其他",
                Location = new Point(12, 268),
                Size = new Size(476, 96),
            };
            group.Controls.Add(LabelAt("最近打开仓库数量", 14, 25, 120));
            numRecentRepos = NumberAt(140, 22, 70, 1, 100, 1);
            group.Controls.Add(numRecentRepos);
            group.Controls.Add(HintAt("1 - 100", 216, 25));

            chkConfirmDangerous = new CheckBox
            {
                Location = new Point(14, 56),
                Size = new Size(450, 24),
                Text = "危险操作前弹出确认（丢弃改动、硬重置、中止合并 / 变基等）",
            };
            group.Controls.Add(chkConfirmDangerous);

            Controls.Add(group);
        }

        private void BuildButtons()
        {
            var btnReset = new Button { Text = "恢复默认", Location = new Point(12, 376), Size = new Size(100, 30) };
            btnReset.Click += (s, e) => ResetToDefaults();

            var btnOpen = new Button { Text = "打开配置文件", Location = new Point(120, 376), Size = new Size(120, 30) };
            btnOpen.Click += (s, e) => OpenConfigFile();

            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(300, 376),
                Size = new Size(86, 30),
                DialogResult = DialogResult.Cancel,
            };

            var btnOk = new Button { Text = "确定", Location = new Point(392, 376), Size = new Size(86, 30) };
            btnOk.Click += (s, e) => SaveAndClose();

            Controls.Add(btnReset);
            Controls.Add(btnOpen);
            Controls.Add(btnCancel);
            Controls.Add(btnOk);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private static Label LabelAt(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 20),
                AutoSize = false,
            };
        }

        private static Label HintAt(string text, int x, int y)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(x, y),
                ForeColor = HintGray,
                AutoSize = true,
            };
            return label;
        }

        private static NumericUpDown NumberAt(int x, int y, int width, decimal min, decimal max, decimal increment)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(width, 23),
                Minimum = min,
                Maximum = max,
                Increment = increment,
                DecimalPlaces = 0,
                ThousandsSeparator = true,
            };
        }

        private static void FillFonts(ComboBox box)
        {
            foreach (var family in new InstalledFontCollection().Families)
                box.Items.Add(family.Name);
            box.Sorted = true;
        }

        // ---------- 值读写 ----------

        private void LoadValues()
        {
            numMaxLines.Value = Clamp(AppSettings.MaxDiffLines, numMaxLines);
            numMaxDiffSizeKB.Value = Clamp(AppSettings.MaxDiffSizeKB, numMaxDiffSizeKB);
            numPreviewKB.Value = Clamp(AppSettings.MaxFilePreviewKB, numPreviewKB);
            numRecentRepos.Value = Clamp(AppSettings.MaxRecentRepos, numRecentRepos);
            chkConfirmDangerous.Checked = AppSettings.ConfirmDangerous;

            var fontName = AppSettings.DiffFontName;
            var index = cboFont.Items.IndexOf(fontName);
            if (index < 0 && !string.IsNullOrEmpty(fontName))
            {
                cboFont.Items.Add(fontName); // 配置里的字体本机没装，也让它显示出来
                index = cboFont.Items.IndexOf(fontName);
            }
            if (index >= 0) cboFont.SelectedIndex = index;

            numFontSize.Value = Clamp(AppSettings.DiffFontSize, numFontSize);
            ApplyPreviewFont();
        }

        private static decimal Clamp(int value, NumericUpDown box)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }

        private void ApplyPreviewFont()
        {
            var name = cboFont.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) name = AppSettings.DefaultDiffFontName;
            var size = (float)numFontSize.Value;
            preview.Font = CreateFont(name, size);
        }

        /// <summary>字体不存在时退回框架默认字体，避免字体被静默替换后看不出差别。</summary>
        public static Font CreateFont(string name, float size)
        {
            try
            {
                var font = new Font(name, size);
                if (!string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    font.Dispose();
                    return new Font(FontFamily.GenericMonospace, size);
                }
                return font;
            }
            catch
            {
                return new Font(FontFamily.GenericMonospace, size);
            }
        }

        private void DrawPreview()
        {
            var sample =
                "--- a/Program.cs\r\n" +
                "+++ b/Program.cs\r\n" +
                "@@ -1,4 +1,5 @@ 命名空间\r\n" +
                " using System;\r\n" +
                "-var old = 1;\r\n" +
                "+var value = 2;\r\n" +
                "+Console.WriteLine(value);\r\n" +
                " return;\r\n";
            DiffRenderer.ApplyTo(preview, DiffRenderer.BuildLines(sample));
        }

        // ---------- 按钮 ----------

        private void ResetToDefaults()
        {
            numMaxLines.Value = Clamp(AppSettings.DefaultMaxDiffLines, numMaxLines);
            numMaxDiffSizeKB.Value = Clamp(AppSettings.DefaultMaxDiffSizeKB, numMaxDiffSizeKB);
            numPreviewKB.Value = Clamp(AppSettings.DefaultMaxFilePreviewKB, numPreviewKB);
            numRecentRepos.Value = Clamp(AppSettings.DefaultMaxRecentRepos, numRecentRepos);
            chkConfirmDangerous.Checked = AppSettings.DefaultConfirmDangerous;
            numFontSize.Value = Clamp(AppSettings.DefaultDiffFontSize, numFontSize);

            var index = cboFont.Items.IndexOf(AppSettings.DefaultDiffFontName);
            if (index >= 0) cboFont.SelectedIndex = index;

            ApplyPreviewFont();
        }

        private void SaveAndClose()
        {
            AppSettings.MaxDiffLines = (int)numMaxLines.Value;
            AppSettings.MaxDiffSizeKB = (int)numMaxDiffSizeKB.Value;
            AppSettings.MaxFilePreviewKB = (int)numPreviewKB.Value;
            AppSettings.MaxRecentRepos = (int)numRecentRepos.Value;
            AppSettings.ConfirmDangerous = chkConfirmDangerous.Checked;

            var fontName = cboFont.SelectedItem as string;
            if (!string.IsNullOrEmpty(fontName)) AppSettings.DiffFontName = fontName;
            AppSettings.DiffFontSize = (int)numFontSize.Value;

            if (!AppSettings.Save())
            {
                MessageBox.Show(this,
                    "配置没能写入：\n" + AppSettings.FilePath + "\n\n请检查该文件是否只读，或目录是否可写。",
                    "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppliedFontName = AppSettings.DiffFontName;
            AppliedFontSize = AppSettings.DiffFontSize;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenConfigFile()
        {
            try
            {
                if (!System.IO.File.Exists(AppSettings.FilePath))
                    AppSettings.Save(); // 文件还没生成时先按当前值写一份
                Process.Start("explorer.exe", "/select,\"" + AppSettings.FilePath + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开配置文件失败：" + ex.Message,
                    "打开配置文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
