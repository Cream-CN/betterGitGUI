using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace guid
{
	public partial class Form1 : Form
	{
		private const string FallbackName = "MyObject";
		private const string HintText = "双击文本框以复制";

		private readonly ToolTip _toolTip = new ToolTip();
		private readonly Timer _statusTimer = new Timer();
		private readonly Icon _appIcon;
		private readonly Bitmap _appIconImage;

		private Guid? _current;
		private int _count;

		public Form1()
		{
			InitializeComponent();
			Icon programIcon = IconLoader.LoadIcon("guid.ico");
			_appIcon = programIcon;
			_appIconImage = IconLoader.LoadBitmap("guid.ico", 64, programIcon);
			Icon = programIcon;

			_statusTimer.Interval = 1600;
			_statusTimer.Tick += StatusTimer_Tick;
			_toolTip.AutoPopDelay = 6000;
			_toolTip.SetToolTip(txtName, "用于 C/C++ 里的类名与常量名；IMPLEMENT_OLECREATE 的 ProgID 取 名称.1");

			txtName.Text = FallbackName;
			lblStatus.Text = HintText;

			Generate();
		}

		/// <summary>F5 随时重新生成，不影响输入框里的名称。</summary>
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.F5)
			{
				Generate();
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private TextBox[] ValueBoxes
		{
			get
			{
				return new TextBox[]
				{
					txtOleCreate,
					txtDefineGuid,
					txtStaticConst,
					txtRegistry,
					txtBracket,
					txtAngle
				};
			}
		}

		/// <summary>生成一个新的 GUID，并同步刷新六种写法。</summary>
		private void Generate()
		{
			_current = GuidFactory.NewGuid();
			_count++;
			lblCount.Text = "已生成 " + _count + " 个";
			Render(_current.Value);
		}

		/// <summary>用同一个 GUID 重新渲染全部格式（改名称或大小写时调用）。</summary>
		private void Render(Guid id)
		{
			string name = GuidFormats.ToIdentifier(txtName.Text, FallbackName);
			bool upper = chkUpper.Checked;

			SetValue(txtOleCreate, GuidFormats.OleCreate(id, name, upper));
			SetValue(txtDefineGuid, GuidFormats.DefineGuid(id, name, upper));
			SetValue(txtStaticConst, GuidFormats.StaticConst(id, name, upper));
			SetValue(txtRegistry, GuidFormats.Registry(id, upper));
			SetValue(txtBracket, GuidFormats.Bracket(id, upper));
			SetValue(txtAngle, GuidFormats.Angle(id, upper));
		}

		private void SetValue(TextBox box, string value)
		{
			box.Text = value;
			_toolTip.SetToolTip(box, value);
		}

		private void btnGenerate_Click(object sender, EventArgs e)
		{
			Generate();
		}

		private void btnCopyAll_Click(object sender, EventArgs e)
		{
			StringBuilder text = new StringBuilder();
			foreach (TextBox box in ValueBoxes)
			{
				text.AppendLine(box.Text);
			}
			CopyToClipboard(text.ToString(), "全部格式");
		}

		/// <summary>关于：显示程序图标、名称与代码来源。</summary>
		private void linkAbout_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (AboutForm about = new AboutForm(_appIcon, _appIconImage))
			{
				about.ShowDialog(this);
			}
		}

		private void CopyButton_Click(object sender, EventArgs e)
		{
			Button button = (Button)sender;
			TextBox box = button.Tag as TextBox;
			if (box != null)
			{
				CopyToClipboard(box.Text, "内容");
			}
		}

		private void ValueBox_DoubleClick(object sender, EventArgs e)
		{
			TextBox box = (TextBox)sender;
			CopyToClipboard(box.Text, "内容");
		}

		/// <summary>进入只读框即全选，方便直接 Ctrl+C。</summary>
		private void ValueBox_Enter(object sender, EventArgs e)
		{
			((TextBox)sender).SelectAll();
		}

		private void txtName_TextChanged(object sender, EventArgs e)
		{
			if (_current.HasValue)
			{
				Render(_current.Value);
			}
		}

		private void chkUpper_CheckedChanged(object sender, EventArgs e)
		{
			if (_current.HasValue)
			{
				Render(_current.Value);
			}
		}

		private void CopyToClipboard(string value, string what)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			try
			{
				Clipboard.SetText(value);
				ShowStatus(what + "已复制到剪贴板");
			}
			catch (Exception ex)
			{
				ShowStatus("复制失败：" + ex.Message);
			}
		}

		private void ShowStatus(string text)
		{
			lblStatus.Text = text;
			lblStatus.ForeColor = Color.FromArgb(21, 128, 61); // 成功提示用绿色
			_statusTimer.Stop();
			_statusTimer.Start();
		}

		private void StatusTimer_Tick(object sender, EventArgs e)
		{
			_statusTimer.Stop();
			lblStatus.Text = HintText;
			lblStatus.ForeColor = SystemColors.GrayText;
		}
	}
}
