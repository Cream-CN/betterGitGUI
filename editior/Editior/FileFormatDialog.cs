using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 文件格式对话框：查看/切换编码（可带 BOM）、换行符，
	/// 并能按所选编码重新读取、只记保存设置，或立即按设置写回文件。
	/// </summary>
	public class FileFormatDialog : Form
	{
		private class EncodingChoice
		{
			public string Name;
			public Encoding Value;
			public bool AllowBom;
			public override string ToString() { return Name; }
		}

		private ComboBox _encodingBox;
		private CheckBox _bomBox;
		private RadioButton _crlfBox;
		private RadioButton _lfBox;
		private Label _hint;

		public Encoding SelectedEncoding = new UTF8Encoding(false);
		public bool SelectedBom;
		public string SelectedLineEnding = "CRLF";
		public bool ReloadNow;
		public bool SaveNow;

		public FileFormatDialog(Document doc)
		{
			Text = "文件格式";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size(480, 306);
			Font = new Font("Microsoft YaHei UI", 9f);

			Label encLabel = new Label();
			encLabel.Text = "编码：";
			encLabel.SetBounds(16, 20, 52, 22);
			Controls.Add(encLabel);

			_encodingBox = new ComboBox();
			_encodingBox.DropDownStyle = ComboBoxStyle.DropDownList;
			_encodingBox.SetBounds(70, 18, 394, 24);
			_encodingBox.Items.Add(MakeChoice("UTF-8", new UTF8Encoding(false), true));
			_encodingBox.Items.Add(MakeChoice("GBK（简体中文）", SafeEncoding(936), false));
			_encodingBox.Items.Add(MakeChoice("GB18030（简体中文，扩展）", SafeEncoding(54936), false));
			_encodingBox.Items.Add(MakeChoice("UTF-16 LE（小端）", new UnicodeEncoding(false, false), true));
			_encodingBox.Items.Add(MakeChoice("UTF-16 BE（大端）", new UnicodeEncoding(true, false), true));
			_encodingBox.Items.Add(MakeChoice("Big5（繁体中文）", SafeEncoding(950), false));
			_encodingBox.Items.Add(MakeChoice("ANSI（系统默认）", Encoding.Default, false));
			_encodingBox.SelectedIndexChanged += delegate { SyncBomState(); };
			Controls.Add(_encodingBox);

			_bomBox = new CheckBox();
			_bomBox.Text = "写入 BOM（只有 UTF-8 / UTF-16 支持）";
			_bomBox.SetBounds(70, 50, 330, 22);
			Controls.Add(_bomBox);

			GroupBox eolGroup = new GroupBox();
			eolGroup.Text = "换行符";
			eolGroup.SetBounds(16, 82, 448, 72);
			Controls.Add(eolGroup);

			_crlfBox = new RadioButton();
			_crlfBox.Text = "CRLF（Windows）";
			_crlfBox.SetBounds(18, 30, 160, 24);
			_crlfBox.Checked = true;
			eolGroup.Controls.Add(_crlfBox);

			_lfBox = new RadioButton();
			_lfBox.Text = "LF（Unix / macOS）";
			_lfBox.SetBounds(200, 30, 180, 24);
			eolGroup.Controls.Add(_lfBox);

			_hint = new Label();
			_hint.SetBounds(16, 160, 448, 64);
			_hint.ForeColor = Color.DimGray;
			Controls.Add(_hint);

			Button reread = new Button();
			reread.Text = "按此编码重新读取";
			reread.SetBounds(16, 262, 146, 30);
			reread.Click += delegate { ReloadNow = true; DialogResult = DialogResult.OK; };
			Controls.Add(reread);

			Button apply = new Button();
			apply.Text = "仅保存设置";
			apply.SetBounds(170, 262, 110, 30);
			apply.Click += delegate { SaveNow = false; DialogResult = DialogResult.OK; };
			Controls.Add(apply);

			Button saveNow = new Button();
			saveNow.Text = "转换并立即保存";
			saveNow.SetBounds(288, 262, 112, 30);
			saveNow.Click += delegate { SaveNow = true; DialogResult = DialogResult.OK; };
			Controls.Add(saveNow);

			Button cancel = new Button();
			cancel.Text = "取消";
			cancel.SetBounds(408, 262, 60, 30);
			cancel.DialogResult = DialogResult.Cancel;
			Controls.Add(cancel);
			CancelButton = cancel;

			if (doc != null)
			{
				SelectEncoding(doc.Encoding);
				_bomBox.Checked = doc.EmitBom;
				if (doc.LineEnding == "LF") _lfBox.Checked = true; else _crlfBox.Checked = true;
				if (doc.FilePath == null) reread.Enabled = false;
			}

			SyncBomState();
		}

		private static Encoding SafeEncoding(int codePage)
		{
			try { return Encoding.GetEncoding(codePage); }
			catch { return Encoding.Default; }
		}

		private EncodingChoice MakeChoice(string name, Encoding value, bool allowBom)
		{
			EncodingChoice choice = new EncodingChoice();
			choice.Name = name;
			choice.Value = value;
			choice.AllowBom = allowBom;
			return choice;
		}

		private void SelectEncoding(Encoding current)
		{
			int fallback = 0;
			for (int i = 0; i < _encodingBox.Items.Count; i++)
			{
				EncodingChoice choice = (EncodingChoice)_encodingBox.Items[i];
				if (choice.Value.CodePage == current.CodePage) { fallback = i; break; }
				if (choice.Value == Encoding.Default) fallback = i;
			}
			_encodingBox.SelectedIndex = fallback;
		}

		private void SyncBomState()
		{
			EncodingChoice choice = _encodingBox.SelectedItem as EncodingChoice;
			bool allow = choice != null && choice.AllowBom;
			_bomBox.Enabled = allow;
			if (!allow) _bomBox.Checked = false;
			UpdateHint();
		}

		private void UpdateHint()
		{
			_hint.Text = "重新读取：按所选编码重新解析磁盘上的文件（有未保存修改会先询问）。\r\n" +
				"仅保存设置：只改下次保存的编码与换行符；立即保存：立刻按设置写回文件。";
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			EncodingChoice choice = _encodingBox.SelectedItem as EncodingChoice;
			if (choice != null)
			{
				SelectedEncoding = choice.Value;
				SelectedBom = _bomBox.Checked;
			}
			SelectedLineEnding = _lfBox.Checked ? "LF" : "CRLF";
			base.OnFormClosing(e);
		}
	}
}
