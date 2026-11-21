using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 数据转代码窗口：位图 → 黑白 / 灰度 / 全彩数组；汇编 → 机器码数组；十六进制 / 文件 → 字节数组，
	/// 统一生成 C / C++ / C# / Java / Python / Rust / Go / JavaScript / GNU as 的常量写法。
	/// </summary>
	public sealed class DataCodeForm : Form
	{
		private const int MaxDisplayChars = 2000000;

		private static readonly CodeLanguage[] LanguageOrder = new CodeLanguage[]
		{
			CodeLanguage.C, CodeLanguage.Cpp, CodeLanguage.CSharp, CodeLanguage.Java, CodeLanguage.Python,
			CodeLanguage.Rust, CodeLanguage.Go, CodeLanguage.JavaScript, CodeLanguage.GasAssembly
		};

		private static readonly string[] LanguageNames = new string[]
		{
			"C（uint8_t / uint16_t / uint32_t）",
			"C++（const / constexpr）",
			"C#（static readonly 数组）",
			"Java（byte[] / int[]）",
			"Python（bytes / list）",
			"Rust（const [u8; N]）",
			"Go（[]byte）",
			"JavaScript（Uint8Array）",
			"GNU as（.byte）"
		};

		private static readonly string[] ImageModeNames = new string[]
		{
			"黑白 · 每像素 1 字节（0 黑 / 1 白）",
			"黑白 · 每像素 1 字节（0 黑 / 255 白）",
			"黑白 · 1bpp 位打包（MSB 先，1 = 白）",
			"黑白 · 1bpp 位打包（LSB 先，1 = 白）",
			"灰度 · 每像素 1 字节（0-255）",
			"全彩 · 0xRRGGBB（32 位）",
			"全彩 · RGB565（16 位，R5G6B5）",
			"全彩 · R / G / B 三个数组"
		};

		private static readonly string[] DitherNames = new string[]
		{
			"无（直接按阈值）",
			"Floyd–Steinberg 误差扩散",
			"Bayer 4×4 有序抖动",
			"Bayer 8×8 有序抖动"
		};

		private static readonly string[] GroupNames = new string[]
		{
			"按指令（CISC）",
			"每 8bit（RISC）",
			"每 16bit",
			"每 32bit",
			"不分组（按每行个数）"
		};

		private static readonly string[] ByteGroupNames = new string[]
		{
			"按原行",
			"每 8bit",
			"每 16bit",
			"每 32bit",
			"不分组（按每行个数）"
		};

		private readonly Action<string> _insertHandler;

		// 图片页
		private TextBox _imagePath;
		private PictureBox _preview;
		private ComboBox _imageMode;
		private NumericUpDown _threshold;
		private TrackBar _thresholdBar;
		private ComboBox _dither;
		private CheckBox _invert;
		private NumericUpDown _maxEdge;
		private ComboBox _background;
		private Label _imageInfo;
		private Bitmap _sourceBitmap;
		private bool _syncThreshold;

		// 汇编页
		private TextBox _asmSource;
		private ComboBox _archBox;
		private ComboBox _syntaxBox;
		private ComboBox _asmGroup;
		private Button _assembleButton;
		private Label _asmInfo;
		private AssembledProgram _assembled;

		// 字节页
		private TextBox _byteText;
		private ComboBox _byteGroup;
		private Label _byteInfo;
		private Label _fileLabel;
		private byte[] _fileBytes;
		private string _fileName;
		private bool _fileLoaded;

		// 输出区
		private ComboBox _languageBox;
		private TextBox _nameBox;
		private ComboBox _styleBox;
		private NumericUpDown _perLine;
		private CheckBox _groupCheck;
		private CheckBox _lengthCheck;
		private CheckBox _staticCheck;
		private CheckBox _constexprCheck;
		private CheckBox _publicCheck;
		private RichTextBox _output;
		private Label _statusLabel;
		private Button _insertButton;

		private Timer _imageTimer;
		private Timer _codeTimer;
		private List<DataPlane> _planes = new List<DataPlane>();
		private string _generatedCode = "";
		private string _sourceKind = "";
		private string _dataComment = "";

		public DataCodeForm() : this(null)
		{
		}

		public DataCodeForm(Action<string> insertHandler)
		{
			_insertHandler = insertHandler;
			InitializeUi();
			LoadAssemblers();
			UpdateImageControls();
			ApplyLanguageOptions();
			UpdateStatus();

			_imageTimer = new Timer();
			_imageTimer.Interval = 220;
			_imageTimer.Tick += delegate
			{
				_imageTimer.Stop();
				UpdateImage();
			};

			_codeTimer = new Timer();
			_codeTimer.Interval = 300;
			_codeTimer.Tick += delegate
			{
				_codeTimer.Stop();
				Regenerate();
			};

			AllowDrop = true;
			DragEnter += ImageDragEnter;
			DragDrop += ImageDragDrop;
		}

		// ---------------- 界面搭建 ----------------

		private void InitializeUi()
		{
			SuspendLayout();
			Text = "数据转代码";
			Font = new Font("Microsoft YaHei UI", 9f);
			ClientSize = new Size(1140, 820);
			MinimumSize = new Size(940, 640);
			StartPosition = FormStartPosition.CenterParent;
			KeyPreview = true;

			TableLayoutPanel root = new TableLayoutPanel();
			root.Dock = DockStyle.Fill;
			root.ColumnCount = 1;
			root.RowCount = 4;
			root.Padding = new Padding(8);
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, 336));
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
			root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
			Controls.Add(root);

			TabControl tabs = new TabControl();
			tabs.Dock = DockStyle.Fill;
			tabs.Margin = new Padding(0, 0, 0, 6);

			TabPage imagePage = new TabPage("图片（位图 → 数组）");
			TabPage assemblyPage = new TabPage("汇编 → 机器码");
			TabPage bytePage = new TabPage("字节（十六进制 / 文件）");
			imagePage.Padding = new Padding(8, 8, 8, 4);
			assemblyPage.Padding = new Padding(8, 8, 8, 4);
			bytePage.Padding = new Padding(8, 8, 8, 4);
			tabs.TabPages.Add(imagePage);
			tabs.TabPages.Add(assemblyPage);
			tabs.TabPages.Add(bytePage);
			tabs.SelectedIndexChanged += delegate { UpdateStatus(); };
			root.Controls.Add(tabs, 0, 0);

			BuildImagePage(imagePage);
			BuildAssemblyPage(assemblyPage);
			BuildBytesPage(bytePage);
			BuildOptionsRow(root);
			BuildOutputBox(root);
			BuildBottomBar(root);

			ResumeLayout();
		}

		private static TableLayoutPanel NewRow(int columns)
		{
			TableLayoutPanel row = new TableLayoutPanel();
			row.Dock = DockStyle.Fill;
			row.ColumnCount = columns;
			row.RowCount = 1;
			row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			row.Margin = new Padding(0, 2, 0, 0);
			return row;
		}

		private static Label NewLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AutoSize = true;
			label.Anchor = AnchorStyles.Left;
			label.Margin = new Padding(0, 0, 4, 0);
			return label;
		}

		private static Button NewButton(string text, EventHandler handler, int width)
		{
			Button button = new Button();
			button.Text = text;
			button.Width = width;
			button.Height = 28;
			button.Anchor = AnchorStyles.Left;
			button.Margin = new Padding(2, 4, 2, 4);
			if (handler != null) button.Click += handler;
			return button;
		}

		private static ComboBox NewCombo(int width, string[] items)
		{
			ComboBox combo = new ComboBox();
			combo.DropDownStyle = ComboBoxStyle.DropDownList;
			combo.Width = width;
			combo.Anchor = AnchorStyles.Left;
			combo.Margin = new Padding(0, 5, 6, 4);
			for (int i = 0; i < items.Length; i++) combo.Items.Add(items[i]);
			if (items.Length > 0) combo.SelectedIndex = 0;
			return combo;
		}

		// ---------------- 图片页 ----------------

		private void BuildImagePage(TabPage page)
		{
			TableLayoutPanel layout = new TableLayoutPanel();
			layout.Dock = DockStyle.Fill;
			layout.ColumnCount = 2;
			layout.RowCount = 5;
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			page.Controls.Add(layout);

			layout.Controls.Add(BuildImagePathRow(), 0, 0);
			layout.Controls.Add(BuildImageModeRow(), 0, 1);
			layout.Controls.Add(BuildDitherRow(), 0, 2);
			layout.Controls.Add(BuildImageScaleRow(), 0, 3);

			_imageInfo = new Label();
			_imageInfo.Dock = DockStyle.Fill;
			_imageInfo.ForeColor = SystemColors.GrayText;
			_imageInfo.TextAlign = ContentAlignment.TopLeft;
			_imageInfo.Text = "打开图片或把图片拖进窗口；这里会显示尺寸、格式与元素数量。";
			layout.Controls.Add(_imageInfo, 0, 4);

			_preview = new PictureBox();
			_preview.Dock = DockStyle.Fill;
			_preview.BorderStyle = BorderStyle.FixedSingle;
			_preview.SizeMode = PictureBoxSizeMode.Zoom;
			_preview.BackColor = Color.White;
			_preview.Margin = new Padding(8, 0, 0, 0);
			_preview.AllowDrop = true;
			_preview.DragEnter += ImageDragEnter;
			_preview.DragDrop += ImageDragDrop;
			layout.Controls.Add(_preview, 1, 0);
			layout.SetRowSpan(_preview, 5);
		}

		private Control BuildImagePathRow()
		{
			TableLayoutPanel row = NewRow(4);
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));

			row.Controls.Add(NewLabel("图片："), 0, 0);
			_imagePath = new TextBox();
			_imagePath.Dock = DockStyle.Fill;
			_imagePath.ReadOnly = true;
			_imagePath.Margin = new Padding(0, 5, 4, 4);
			row.Controls.Add(_imagePath, 1, 0);
			row.Controls.Add(NewButton("浏览…", delegate { BrowseImage(); }, 82), 2, 0);
			row.Controls.Add(NewButton("清除", delegate { ClearImage(); }, 64), 3, 0);
			return row;
		}

		private Control BuildImageModeRow()
		{
			TableLayoutPanel row = NewRow(2);
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			row.Controls.Add(NewLabel("输出形式："), 0, 0);
			_imageMode = NewCombo(520, ImageModeNames);
			_imageMode.Dock = DockStyle.Fill;
			_imageMode.Width = 520;
			_imageMode.SelectedIndexChanged += delegate
			{
				UpdateImageControls();
				ScheduleImageUpdate();
			};
			row.Controls.Add(_imageMode, 1, 0);
			return row;
		}

		private Control BuildDitherRow()
		{
			TableLayoutPanel row = NewRow(6);
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));

			row.Controls.Add(NewLabel("阈值："), 0, 0);
			_threshold = new NumericUpDown();
			_threshold.Minimum = 1;
			_threshold.Maximum = 255;
			_threshold.Value = 128;
			_threshold.Width = 56;
			_threshold.Anchor = AnchorStyles.Left;
			_threshold.Margin = new Padding(0, 5, 4, 4);
			row.Controls.Add(_threshold, 1, 0);

			_thresholdBar = new TrackBar();
			_thresholdBar.Minimum = 1;
			_thresholdBar.Maximum = 255;
			_thresholdBar.TickFrequency = 16;
			_thresholdBar.Value = 128;
			_thresholdBar.Dock = DockStyle.Fill;
			_thresholdBar.Height = 26;
			_thresholdBar.Margin = new Padding(0, 4, 8, 4);
			row.Controls.Add(_thresholdBar, 2, 0);

			row.Controls.Add(NewLabel("抖动："), 3, 0);
			_dither = NewCombo(182, DitherNames);
			_dither.Dock = DockStyle.Fill;
			_dither.SelectedIndexChanged += delegate { ScheduleImageUpdate(); };
			row.Controls.Add(_dither, 4, 0);

			_invert = new CheckBox();
			_invert.Text = "反色";
			_invert.AutoSize = true;
			_invert.Anchor = AnchorStyles.Left;
			_invert.Margin = new Padding(4, 8, 0, 4);
			_invert.CheckedChanged += delegate { ScheduleImageUpdate(); };
			row.Controls.Add(_invert, 5, 0);

			_threshold.ValueChanged += delegate
			{
				if (_syncThreshold) return;
				_syncThreshold = true;
				_thresholdBar.Value = (int)_threshold.Value;
				_syncThreshold = false;
				ScheduleImageUpdate();
			};
			_thresholdBar.ValueChanged += delegate
			{
				if (_syncThreshold) return;
				_syncThreshold = true;
				_threshold.Value = _thresholdBar.Value;
				_syncThreshold = false;
				ScheduleImageUpdate();
			};
			return row;
		}

		private Control BuildImageScaleRow()
		{
			TableLayoutPanel row = NewRow(6);
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			row.Controls.Add(NewLabel("最长边："), 0, 0);
			_maxEdge = new NumericUpDown();
			_maxEdge.Minimum = 0;
			_maxEdge.Maximum = 4096;
			_maxEdge.Value = 0;
			_maxEdge.Width = 56;
			_maxEdge.Anchor = AnchorStyles.Left;
			_maxEdge.Margin = new Padding(0, 5, 4, 4);
			_maxEdge.ValueChanged += delegate { ScheduleImageUpdate(); };
			row.Controls.Add(_maxEdge, 1, 0);

			Label edgeHint = NewLabel("px（0 = 原样）");
			edgeHint.ForeColor = SystemColors.GrayText;
			row.Controls.Add(edgeHint, 2, 0);

			row.Controls.Add(NewLabel("透明背景："), 3, 0);
			_background = NewCombo(200, new string[] { "白色（透明合成到白底）", "黑色（透明合成到黑底）" });
			_background.Dock = DockStyle.Fill;
			_background.SelectedIndexChanged += delegate { ScheduleImageUpdate(); };
			row.Controls.Add(_background, 4, 0);

			Label hint = NewLabel("仅黑白模式使用阈值 / 抖动；阈值可拖动。");
			hint.ForeColor = SystemColors.GrayText;
			row.Controls.Add(hint, 5, 0);
			return row;
		}

		private void UpdateImageControls()
		{
			if (_imageMode == null) return;
			bool blackWhite = _imageMode.SelectedIndex >= 0 && _imageMode.SelectedIndex <= 3;
			_threshold.Enabled = blackWhite;
			_thresholdBar.Enabled = blackWhite;
			_dither.Enabled = blackWhite;
		}

		private ImageConvertOptions ReadImageOptions()
		{
			ImageConvertOptions options = new ImageConvertOptions();
			options.Mode = (ImageOutputMode)Math.Max(0, _imageMode.SelectedIndex);
			options.Threshold = (int)_threshold.Value;
			options.Dither = (DitherMode)Math.Max(0, _dither.SelectedIndex);
			options.Invert = _invert.Checked;
			options.MaxEdge = (int)_maxEdge.Value;
			options.Background = _background.SelectedIndex == 1 ? Color.Black : Color.White;
			return options;
		}

		private void BrowseImage()
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "选择图片";
				dialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|所有文件|*.*";
				if (dialog.ShowDialog(this) != DialogResult.OK) return;
				LoadImage(dialog.FileName);
			}
		}

		private void LoadImage(string path)
		{
			try
			{
				Bitmap bitmap;
				using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
				using (Image image = Image.FromStream(stream, true, false))
				{
					bitmap = new Bitmap(image);
				}
				if (_sourceBitmap != null) _sourceBitmap.Dispose();
				_sourceBitmap = bitmap;
				_imagePath.Text = path;
				UpdateImage();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开图片失败：" + ex.Message, "图片", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void ClearImage()
		{
			if (_sourceBitmap != null)
			{
				_sourceBitmap.Dispose();
				_sourceBitmap = null;
			}
			Image old = _preview.Image;
			_preview.Image = null;
			if (old != null) old.Dispose();

			_imagePath.Text = "";
			_imageInfo.ForeColor = SystemColors.GrayText;
			_imageInfo.Text = "已清除图片。";
			if (_sourceKind == "image")
			{
				_planes = new List<DataPlane>();
				_generatedCode = "";
				_output.Text = "";
				_sourceKind = "";
				_dataComment = "";
				UpdateStatus();
			}
		}

		private void ScheduleImageUpdate()
		{
			if (_sourceBitmap == null) return;
			if (_imageTimer == null) return;
			_imageTimer.Stop();
			_imageTimer.Start();
		}

		private void UpdateImage()
		{
			if (_sourceBitmap == null) return;
			try
			{
				ImageConvertResult result = ImageDataConverter.Convert(_sourceBitmap, ReadImageOptions());
				Image old = _preview.Image;
				_preview.Image = result.Preview;
				if (old != null) old.Dispose();

				_imageInfo.ForeColor = SystemColors.GrayText;
				_imageInfo.Text = result.Summary + (result.Height != _sourceBitmap.Height || result.Width != _sourceBitmap.Width
					? "（原图 " + _sourceBitmap.Width + " × " + _sourceBitmap.Height + "）" : "");

				_planes = result.Planes;
				_sourceKind = "image";
				_dataComment = result.Summary;
				Regenerate();
			}
			catch (Exception ex)
			{
				_imageInfo.ForeColor = Color.FromArgb(180, 30, 30);
				_imageInfo.Text = "转换失败：" + ex.Message;
			}
		}

		private void ImageDragEnter(object sender, DragEventArgs e)
		{
			string[] files = e.Data == null ? null : (e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null);
			e.Effect = files != null && files.Length > 0 && IsImageFile(files[0]) ? DragDropEffects.Copy : DragDropEffects.None;
		}

		private void ImageDragDrop(object sender, DragEventArgs e)
		{
			string[] files = e.Data == null ? null : (e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null);
			if (files == null || files.Length == 0) return;
			if (!IsImageFile(files[0])) return;
			LoadImage(files[0]);
		}

		private static bool IsImageFile(string path)
		{
			if (string.IsNullOrEmpty(path)) return false;
			string extension = Path.GetExtension(path).ToLowerInvariant();
			return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" ||
				extension == ".gif" || extension == ".tif" || extension == ".tiff" || extension == ".ico";
		}

		// ---------------- 汇编页 ----------------

		private void BuildAssemblyPage(TabPage page)
		{
			TableLayoutPanel layout = new TableLayoutPanel();
			layout.Dock = DockStyle.Fill;
			layout.ColumnCount = 1;
			layout.RowCount = 3;
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
			page.Controls.Add(layout);

			TableLayoutPanel bar = NewRow(9);
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 268));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			bar.Controls.Add(NewLabel("架构："), 0, 0);
			_archBox = new ComboBox();
			_archBox.DropDownStyle = ComboBoxStyle.DropDownList;
			_archBox.Dock = DockStyle.Fill;
			_archBox.Margin = new Padding(0, 5, 6, 4);
			_archBox.DisplayMember = "Display";
			_archBox.SelectedIndexChanged += delegate { ArchChanged(); };
			bar.Controls.Add(_archBox, 1, 0);

			bar.Controls.Add(NewLabel("语法："), 2, 0);
			_syntaxBox = NewCombo(92, new string[] { "Intel", "AT&T" });
			_syntaxBox.Dock = DockStyle.Fill;
			bar.Controls.Add(_syntaxBox, 3, 0);

			bar.Controls.Add(NewLabel("分组："), 4, 0);
			_asmGroup = NewCombo(162, GroupNames);
			_asmGroup.Dock = DockStyle.Fill;
			bar.Controls.Add(_asmGroup, 5, 0);

			_assembleButton = NewButton("汇编并生成", delegate { AssembleAndGenerate(); }, 110);
			bar.Controls.Add(_assembleButton, 6, 0);
			bar.Controls.Add(NewButton("重新探测", delegate { LoadAssemblers(); }, 88), 7, 0);

			_asmInfo = new Label();
			_asmInfo.Dock = DockStyle.Fill;
			_asmInfo.ForeColor = SystemColors.GrayText;
			_asmInfo.AutoEllipsis = true;
			_asmInfo.TextAlign = ContentAlignment.MiddleLeft;
			_asmInfo.Margin = new Padding(6, 0, 0, 0);
			bar.Controls.Add(_asmInfo, 8, 0);

			layout.Controls.Add(bar, 0, 0);

			_asmSource = new TextBox();
			_asmSource.Dock = DockStyle.Fill;
			_asmSource.Multiline = true;
			_asmSource.AcceptsTab = true;
			_asmSource.ScrollBars = ScrollBars.Both;
			_asmSource.WordWrap = false;
			_asmSource.Font = new Font("Consolas", 9.75f);
			_asmSource.Text =
				"; x86 示例（Intel 语法）。只写指令与标签，不要写 .section / .text。\r\n" +
				"    push rbp\r\n" +
				"    mov rbp, rsp\r\n" +
				"    xor eax, eax\r\n" +
				"    add eax, 42\r\n" +
				"    pop rbp\r\n" +
				"    ret\r\n";
			layout.Controls.Add(_asmSource, 0, 1);

			Label hint = NewLabel("本机装有 GNU as / objdump 即可汇编（自动探测 MSYS2、MinGW、Cygwin 与交叉工具链）。汇编结果末尾的对齐补位会自动丢弃。");
			hint.ForeColor = SystemColors.GrayText;
			layout.Controls.Add(hint, 0, 2);
		}

		private void LoadAssemblers()
		{
			List<AssemblerTarget> targets = AssemblerCatalog.Detect();
			_archBox.BeginUpdate();
			_archBox.Items.Clear();
			for (int i = 0; i < targets.Count; i++) _archBox.Items.Add(targets[i]);
			_archBox.EndUpdate();

			int index = 0;
			for (int i = 0; i < targets.Count; i++)
			{
				if (targets[i].Available)
				{
					index = i;
					break;
				}
			}
			_archBox.SelectedIndex = index;
			ArchChanged();
		}

		private void ArchChanged()
		{
			AssemblerTarget target = _archBox.SelectedItem as AssemblerTarget;
			if (target == null) return;

			_syntaxBox.Enabled = target.X86;
			if (target.Risc) _asmGroup.SelectedIndex = 1;
			else if (target.X86) _asmGroup.SelectedIndex = 0;
			_assembleButton.Enabled = target.Available;
			_asmInfo.ForeColor = target.Available ? SystemColors.GrayText : Color.FromArgb(180, 30, 30);
			_asmInfo.Text = target.Detail;
		}

		private GroupingMode ReadAsmGrouping()
		{
			switch (_asmGroup.SelectedIndex)
			{
				case 1: return GroupingMode.PerByte;
				case 2: return GroupingMode.PerWord;
				case 3: return GroupingMode.PerDWord;
				case 4: return GroupingMode.None;
				default: return GroupingMode.PerInstruction;
			}
		}

		private void AssembleAndGenerate()
		{
			AssemblerTarget target = _archBox.SelectedItem as AssemblerTarget;
			if (target == null) return;

			_assembleButton.Enabled = false;
			Cursor previous = Cursor;
			Cursor = Cursors.WaitCursor;
			try
			{
				AssembledProgram result = AssemblerCatalog.Assemble(target, _syntaxBox.SelectedItem as string, _asmSource.Text);
				if (result.Error != null)
				{
					_asmInfo.ForeColor = Color.FromArgb(180, 30, 30);
					_asmInfo.Text = "汇编失败：" + FirstLine(result.Error);
					MessageBox.Show(this, result.Error, "汇编失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				_assembled = result;
				_planes = new List<DataPlane>();
				_planes.Add(BytePlaneBuilder.FromBytes(result.Bytes, result.Instructions, ReadAsmGrouping(), (int)_perLine.Value));
				_sourceKind = "assembly";
				_dataComment = result.Summary + (string.IsNullOrEmpty(result.Warning) ? "" : " · " + result.Warning);
				_asmInfo.ForeColor = SystemColors.GrayText;
				_asmInfo.Text = result.Summary + (string.IsNullOrEmpty(result.Warning) ? "" : "（" + result.Warning + "）");
				Regenerate();
			}
			finally
			{
				Cursor = previous;
				_assembleButton.Enabled = target.Available;
			}
		}

		// ---------------- 字节页 ----------------

		private void BuildBytesPage(TabPage page)
		{
			TableLayoutPanel layout = new TableLayoutPanel();
			layout.Dock = DockStyle.Fill;
			layout.ColumnCount = 1;
			layout.RowCount = 3;
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
			page.Controls.Add(layout);

			TableLayoutPanel bar = NewRow(5);
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 186));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));

			bar.Controls.Add(NewButton("从文件载入…", delegate { LoadByteFile(); }, 122), 0, 0);
			_fileLabel = new Label();
			_fileLabel.Text = "尚未载入文件";
			_fileLabel.ForeColor = SystemColors.GrayText;
			_fileLabel.Dock = DockStyle.Fill;
			_fileLabel.TextAlign = ContentAlignment.MiddleLeft;
			_fileLabel.AutoEllipsis = true;
			_fileLabel.Margin = new Padding(4, 0, 8, 0);
			bar.Controls.Add(_fileLabel, 1, 0);

			bar.Controls.Add(NewLabel("分组："), 2, 0);
			_byteGroup = NewCombo(180, ByteGroupNames);
			_byteGroup.Dock = DockStyle.Fill;
			_byteGroup.SelectedIndexChanged += delegate { ByteGroupChanged(); };
			bar.Controls.Add(_byteGroup, 3, 0);
			bar.Controls.Add(NewButton("解析文本", delegate { ParseByteText(true); }, 108), 4, 0);

			layout.Controls.Add(bar, 0, 0);

			_byteText = new TextBox();
			_byteText.Dock = DockStyle.Fill;
			_byteText.Multiline = true;
			_byteText.AcceptsTab = true;
			_byteText.ScrollBars = ScrollBars.Both;
			_byteText.WordWrap = false;
			_byteText.Font = new Font("Consolas", 9.75f);
			_byteText.Text =
				"55 48 89 E5\r\n" +
				"31 C0\r\n" +
				"5D\r\n" +
				"C3\r\n";
			_byteText.TextChanged += delegate
			{
				if (!_fileLoaded) ParseByteText(false);
			};
			layout.Controls.Add(_byteText, 0, 1);

			_byteInfo = new Label();
			_byteInfo.ForeColor = SystemColors.GrayText;
			_byteInfo.Dock = DockStyle.Fill;
			_byteInfo.Text = "支持 0x55 / 55h / \\x55 / 0b10101010 / 裸两位十六进制；也可以直接载入 .bin、.hex 文件。";
			_byteInfo.TextAlign = ContentAlignment.MiddleLeft;
			layout.Controls.Add(_byteInfo, 0, 2);
		}

		private GroupingMode ReadByteGrouping()
		{
			switch (_byteGroup.SelectedIndex)
			{
				case 1: return GroupingMode.PerByte;
				case 2: return GroupingMode.PerWord;
				case 3: return GroupingMode.PerDWord;
				case 4: return GroupingMode.None;
				default: return GroupingMode.PerInstruction;
			}
		}

		private void ParseByteText(bool reportError)
		{
			byte[] bytes;
			List<int[]> groups;
			string error;
			if (!ByteTextParser.TryParse(_byteText.Text, out bytes, out groups, out error))
			{
				_byteInfo.ForeColor = Color.FromArgb(180, 30, 30);
				_byteInfo.Text = error;
				if (reportError) MessageBox.Show(this, error, "解析失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			_fileLoaded = false;
			_fileBytes = null;
			_fileName = null;
			_planes = new List<DataPlane>();
			_planes.Add(BytePlaneBuilder.FromBytes(bytes, groups, ReadByteGrouping(), (int)_perLine.Value));
			_sourceKind = "bytes";
			_dataComment = "十六进制文本 · " + bytes.Length + " 字节";
			_byteInfo.ForeColor = SystemColors.GrayText;
			_byteInfo.Text = "已解析 " + bytes.Length + " 字节，按原行分成 " + groups.Count + " 行。";
			Regenerate();
		}

		private void LoadByteFile()
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "载入二进制文件";
				dialog.Filter = "二进制 / 十六进制文件|*.bin;*.hex;*.dat;*.rom;*.img;*.s19;*.ihex;*.txt|所有文件|*.*";
				if (dialog.ShowDialog(this) != DialogResult.OK) return;

				try
				{
					byte[] bytes = File.ReadAllBytes(dialog.FileName);
					_fileBytes = bytes;
					_fileName = Path.GetFileName(dialog.FileName);
					_fileLoaded = true;
					_fileLabel.Text = _fileName + "（" + bytes.Length + " 字节）";
					_planes = new List<DataPlane>();
					_planes.Add(BytePlaneBuilder.FromBytes(bytes, null, ReadByteGrouping(), (int)_perLine.Value));
					_sourceKind = "bytes";
					_dataComment = _fileName + " · " + bytes.Length + " 字节";
					_byteInfo.ForeColor = SystemColors.GrayText;
					_byteInfo.Text = "已载入 " + _fileName + "：" + bytes.Length + " 字节（按当前分组规则生成）。";
					Regenerate();
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "读取文件失败：" + ex.Message, "字节文件", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void ByteGroupChanged()
		{
			if (_fileLoaded && _fileBytes != null)
			{
				_planes = new List<DataPlane>();
				_planes.Add(BytePlaneBuilder.FromBytes(_fileBytes, null, ReadByteGrouping(), (int)_perLine.Value));
				_sourceKind = "bytes";
				Regenerate();
			}
			else
			{
				ParseByteText(false);
			}
		}

		// ---------------- 输出区 ----------------

		private void BuildOptionsRow(TableLayoutPanel root)
		{
			FlowLayoutPanel flow = new FlowLayoutPanel();
			flow.Dock = DockStyle.Fill;
			flow.WrapContents = true;
			flow.Padding = new Padding(2, 6, 2, 0);

			flow.Controls.Add(OptionLabel("语言："));
			_languageBox = NewCombo(232, LanguageNames);
			_languageBox.Margin = new Padding(0, 8, 10, 4);
			_languageBox.SelectedIndexChanged += delegate
			{
				ApplyLanguageOptions();
				ScheduleRegenerate();
			};
			flow.Controls.Add(_languageBox);

			flow.Controls.Add(OptionLabel("变量名："));
			_nameBox = new TextBox();
			_nameBox.Text = "image_data";
			_nameBox.Width = 170;
			_nameBox.Margin = new Padding(0, 9, 10, 4);
			_nameBox.TextChanged += delegate { ScheduleRegenerate(); };
			flow.Controls.Add(_nameBox);

			flow.Controls.Add(OptionLabel("数值："));
			_styleBox = NewCombo(96, new string[] { "十六进制", "十进制", "二进制" });
			_styleBox.Margin = new Padding(0, 8, 10, 4);
			_styleBox.SelectedIndexChanged += delegate { ScheduleRegenerate(); };
			flow.Controls.Add(_styleBox);

			flow.Controls.Add(OptionLabel("每行："));
			_perLine = new NumericUpDown();
			_perLine.Minimum = 1;
			_perLine.Maximum = 64;
			_perLine.Value = 12;
			_perLine.Width = 56;
			_perLine.Margin = new Padding(0, 9, 12, 4);
			_perLine.ValueChanged += delegate { ScheduleRegenerate(); };
			flow.Controls.Add(_perLine);

			_groupCheck = NewCheck("按行分组（图像行 / 指令）", true, true);
			_lengthCheck = NewCheck("输出长度常量", true, true);
			_staticCheck = NewCheck("static", false, true);
			_constexprCheck = NewCheck("constexpr", false, true);
			_publicCheck = NewCheck("public", false, true);

			flow.Controls.Add(_groupCheck);
			flow.Controls.Add(_lengthCheck);
			flow.Controls.Add(_staticCheck);
			flow.Controls.Add(_constexprCheck);
			flow.Controls.Add(_publicCheck);

			root.Controls.Add(flow, 0, 1);
		}

		private Label OptionLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AutoSize = true;
			label.Margin = new Padding(4, 11, 2, 4);
			return label;
		}

		private CheckBox NewCheck(string text, bool check, bool schedule)
		{
			CheckBox box = new CheckBox();
			box.Text = text;
			box.Checked = check;
			box.AutoSize = true;
			box.Margin = new Padding(6, 10, 6, 4);
			if (schedule) box.CheckedChanged += delegate { ScheduleRegenerate(); };
			return box;
		}

		private void ApplyLanguageOptions()
		{
			CodeLanguage language = LanguageOrder[Math.Max(0, _languageBox.SelectedIndex)];
			_staticCheck.Enabled = language == CodeLanguage.C || language == CodeLanguage.Cpp;
			_constexprCheck.Enabled = language == CodeLanguage.Cpp;
			_publicCheck.Enabled = language == CodeLanguage.CSharp || language == CodeLanguage.Java || language == CodeLanguage.Rust;
		}

		private void BuildOutputBox(TableLayoutPanel root)
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Fill;
			panel.Padding = new Padding(2, 2, 2, 2);

			_output = new RichTextBox();
			_output.Dock = DockStyle.Fill;
			_output.ReadOnly = true;
			_output.WordWrap = false;
			_output.ScrollBars = RichTextBoxScrollBars.ForcedBoth;
			_output.Font = new Font("Consolas", 9.5f);
			_output.BackColor = Color.White;
			_output.DetectUrls = false;
			_output.BorderStyle = BorderStyle.FixedSingle;
			panel.Controls.Add(_output);

			root.Controls.Add(panel, 0, 2);
		}

		private void BuildBottomBar(TableLayoutPanel root)
		{
			TableLayoutPanel bar = new TableLayoutPanel();
			bar.Dock = DockStyle.Fill;
			bar.ColumnCount = 2;
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			_statusLabel = new Label();
			_statusLabel.Dock = DockStyle.Fill;
			_statusLabel.TextAlign = ContentAlignment.MiddleLeft;
			_statusLabel.ForeColor = SystemColors.GrayText;
			_statusLabel.Margin = new Padding(2, 0, 8, 0);
			bar.Controls.Add(_statusLabel, 0, 0);

			FlowLayoutPanel buttons = new FlowLayoutPanel();
			buttons.Dock = DockStyle.Fill;
			buttons.FlowDirection = FlowDirection.RightToLeft;
			buttons.WrapContents = false;
			buttons.AutoSize = true;

			Button close = NewButton("关闭", delegate { Close(); }, 76);
			_insertButton = NewButton("插入到编辑器", delegate { InsertCode(); }, 112);
			_insertButton.Enabled = _insertHandler != null;
			Button save = NewButton("保存…", delegate { SaveCode(); }, 82);
			Button copy = NewButton("复制", delegate { CopyCode(); }, 70);

			buttons.Controls.Add(close);
			buttons.Controls.Add(_insertButton);
			buttons.Controls.Add(save);
			buttons.Controls.Add(copy);
			bar.Controls.Add(buttons, 1, 0);

			root.Controls.Add(bar, 0, 3);
		}

		private CodeGenerateOptions ReadCodeOptions()
		{
			CodeGenerateOptions options = new CodeGenerateOptions();
			options.Language = LanguageOrder[Math.Max(0, _languageBox.SelectedIndex)];
			options.Name = _nameBox.Text;
			options.Style = (NumberStyle)Math.Max(0, _styleBox.SelectedIndex);
			options.PerLine = (int)_perLine.Value;
			options.UseGroups = _groupCheck.Checked;
			options.EmitLength = _lengthCheck.Checked;
			options.Static = _staticCheck.Checked;
			options.Constexpr = _constexprCheck.Checked;
			options.Public = _publicCheck.Checked;
			options.Comment = _dataComment;
			return options;
		}

		private void ScheduleRegenerate()
		{
			if (_codeTimer == null) return;
			_codeTimer.Stop();
			_codeTimer.Start();
		}

		private void Regenerate()
		{
			if (_planes == null || _planes.Count == 0)
			{
				_generatedCode = "";
				_output.Text = "";
				UpdateStatus();
				return;
			}

			try
			{
				_generatedCode = CodeGenerator.Generate(_planes, ReadCodeOptions());
			}
			catch (Exception ex)
			{
				_generatedCode = "";
				_output.Text = "生成失败：" + ex.Message;
				return;
			}

			if (_generatedCode.Length > MaxDisplayChars)
				_output.Text = _generatedCode.Substring(0, MaxDisplayChars) + "\r\n/* … 内容过长，已截断显示；复制 / 保存仍为完整代码 … */";
			else
				_output.Text = _generatedCode;

			try { _output.Select(0, 0); }
			catch { }
			UpdateStatus();
		}

		private void UpdateStatus()
		{
			if (_statusLabel == null) return;
			if (_planes == null || _planes.Count == 0)
			{
				_statusLabel.Text = "等待数据：打开图片、汇编代码，或粘贴十六进制字节。";
				return;
			}

			int elements = 0;
			for (int i = 0; i < _planes.Count; i++) elements += _planes[i].Count;
			string kind = _sourceKind == "image" ? "位图" : (_sourceKind == "assembly" ? "汇编机器码" : "字节");

			StringBuilder text = new StringBuilder();
			text.Append(kind).Append(" · ").Append(elements.ToString("N0", CultureInfo.InvariantCulture)).Append(" 个元素");
			if (_planes.Count > 1) text.Append(" · ").Append(_planes.Count).Append(" 个数组");
			text.Append(" · 代码 ").Append(_generatedCode.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(" 字符");
			if (_generatedCode.Length > MaxDisplayChars)
				text.Append(" · 仅显示前 ").Append(MaxDisplayChars.ToString("N0", CultureInfo.InvariantCulture));
			_statusLabel.Text = text.ToString();
		}

		private void CopyCode()
		{
			if (_generatedCode.Length == 0) return;
			try
			{
				Clipboard.SetText(_generatedCode);
				ShowStatus("已复制 " + _generatedCode.Length + " 字符到剪贴板。");
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "复制失败：" + ex.Message, "复制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private void SaveCode()
		{
			if (_generatedCode.Length == 0) return;
			CodeLanguage language = LanguageOrder[Math.Max(0, _languageBox.SelectedIndex)];
			string extension = DefaultExtension(language);
			string fileName = CodeGenerator.MakeIdentifier(_nameBox.Text) + extension;

			using (SaveFileDialog dialog = new SaveFileDialog())
			{
				dialog.Title = "保存生成的代码";
				dialog.FileName = fileName;
				dialog.Filter = "代码文件|*" + extension + "|文本文件|*.txt|所有文件|*.*";
				dialog.AddExtension = true;
				if (dialog.ShowDialog(this) != DialogResult.OK) return;

				try
				{
					File.WriteAllText(dialog.FileName, _generatedCode, new UTF8Encoding(false));
					ShowStatus("已保存：" + dialog.FileName);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "保存失败：" + ex.Message, "保存", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void InsertCode()
		{
			if (_insertHandler == null || _generatedCode.Length == 0) return;
			try
			{
				_insertHandler(_generatedCode);
				ShowStatus("已插入 " + _generatedCode.Length + " 字符到编辑器。");
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "插入失败：" + ex.Message, "插入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private static string DefaultExtension(CodeLanguage language)
		{
			switch (language)
			{
				case CodeLanguage.C: return ".h";
				case CodeLanguage.Cpp: return ".cpp";
				case CodeLanguage.CSharp: return ".cs";
				case CodeLanguage.Java: return ".java";
				case CodeLanguage.Python: return ".py";
				case CodeLanguage.Rust: return ".rs";
				case CodeLanguage.Go: return ".go";
				case CodeLanguage.JavaScript: return ".js";
				default: return ".s";
			}
		}

		private void ShowStatus(string text)
		{
			if (_statusLabel == null) return;
			_statusLabel.ForeColor = Color.FromArgb(21, 128, 61);
			_statusLabel.Text = text;
		}

		private static string FirstLine(string text)
		{
			if (string.IsNullOrEmpty(text)) return "";
			int index = text.IndexOfAny(new char[] { '\r', '\n' });
			return index < 0 ? text : text.Substring(0, index);
		}
	}
}
